using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Reforged.Server;

public sealed class ServerNetworkHost
{
    private readonly ServerRuntimeOptions _options;
    private readonly Func<IReadOnlyDictionary<string, RuntimeWorld>> _worldProvider;
    private readonly RuntimeCommandDispatcher _commands;
    private readonly RuntimeSessionManager _sessions;
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeAccountSecurityService _accountSecurity;

    public ServerNetworkHost(
        ServerRuntimeOptions options,
        Func<IReadOnlyDictionary<string, RuntimeWorld>> worldProvider,
        RuntimeCommandDispatcher commands,
        RuntimeSessionManager sessions,
        RuntimeCharacterService characters)
    {
        _options = options;
        _worldProvider = worldProvider;
        _commands = commands;
        _sessions = sessions;
        _characters = characters;
        _accountSecurity = new RuntimeAccountSecurityService(characters.ProjectRoot);
    }

    public async Task RunTelnetAsync(CancellationToken cancellationToken)
    {
        var endpoint = new IPEndPoint(_options.Network.ResolveBindAddress(), _options.Network.TelnetPort);
        var listener = new TcpListener(endpoint);
        listener.Start();
        Console.WriteLine($"Telnet listener started on {endpoint.Address}:{endpoint.Port}; maxSessions={_options.Network.MaxSessions}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken hostCancellationToken)
    {
        var sessionId = $"telnet-{Guid.NewGuid():N}"[..20];
        using var connection = client;
        using var stream = connection.GetStream();
        await using var telnet = new TelnetConnection(stream);

        using var loginTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(10, _options.Network.LoginTimeoutSeconds)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(hostCancellationToken, loginTimeout.Token);

        try
        {
            await telnet.NegotiateAsync(linked.Token).ConfigureAwait(false);
            await WriteBannerAsync(telnet, linked.Token).ConfigureAwait(false);
            var account = await AuthenticateAccountAsync(telnet, linked.Token).ConfigureAwait(false);
            if (account is null) return;

            var selectedWorld = await SelectWorldAsync(telnet, linked.Token).ConfigureAwait(false);
            if (selectedWorld is null)
            {
                await telnet.WriteLineAsync("Connection closed: no world selected.", linked.Token).ConfigureAwait(false);
                return;
            }

            // Character selection, creation, and its ANSI editor must not inherit the login timeout.
            // Only Host shutdown or a real socket disconnect may cancel this phase.
            var character = await SelectOrCreateCharacterAsync(account, selectedWorld, telnet, hostCancellationToken).ConfigureAwait(false);
            if (character is null)
            {
                await telnet.WriteLineAsync("Connection closed: no character selected.", linked.Token).ConfigureAwait(false);
                return;
            }

            var session = _sessions.CreateCharacterSession(sessionId, selectedWorld, character);
            if (!_sessions.TryAdd(session, Math.Max(1, _options.Network.MaxSessions), out var message))
            {
                await telnet.WriteLineAsync($"Connection refused: {message}", linked.Token).ConfigureAwait(false);
                return;
            }

            try
            {
                await telnet.WriteLineAsync(selectedWorld.Definition.Motd, hostCancellationToken).ConfigureAwait(false);
                await telnet.WriteLineAsync(_commands.Dispatch(session, "look"), hostCancellationToken).ConfigureAwait(false);
                Task<string?>? pendingInput = null;
                while (!hostCancellationToken.IsCancellationRequested && connection.Connected)
                {
                    foreach (var pending in session.DrainMessages())
                        await telnet.WriteLineAsync(pending, hostCancellationToken).ConfigureAwait(false);

                    // MegaMUD path execution waits for the native MajorMUD-style status prompt.
                    // Do not append a generic "> " prompt after room output; it prevents the client
                    // from recognizing that movement completed and stalls imported .mp paths.
                    if (pendingInput is null)
                    {
                        await telnet.WriteAsync(_commands.RenderClassicPrompt(session) + " ", hostCancellationToken).ConfigureAwait(false);
                        pendingInput = telnet.ReadLineAsync(hideInput: false, hostCancellationToken);
                    }

                    var completed = await Task.WhenAny(pendingInput, Task.Delay(100, hostCancellationToken)).ConfigureAwait(false);
                    if (!ReferenceEquals(completed, pendingInput))
                    {
                        var liveMessages = session.DrainMessages();
                        if (liveMessages.Count > 0)
                        {
                            await telnet.WriteLineAsync(hostCancellationToken).ConfigureAwait(false);
                            foreach (var pending in liveMessages)
                                await telnet.WriteLineAsync(pending, hostCancellationToken).ConfigureAwait(false);
                            await telnet.WriteAsync(_commands.RenderClassicPrompt(session) + " ", hostCancellationToken).ConfigureAwait(false);
                        }
                        continue;
                    }

                    var input = await pendingInput.ConfigureAwait(false);
                    pendingInput = null;
                    if (input is null) break;

                    if (_commands.RequestsSessionExit(session, input))
                    {
                        await telnet.WriteLineAsync($"Exiting MajorMUD in {RuntimeClassicDeathLawPvpAuthority.GracefulExitDelaySeconds} seconds...", hostCancellationToken).ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromSeconds(RuntimeClassicDeathLawPvpAuthority.GracefulExitDelaySeconds), hostCancellationToken).ConfigureAwait(false);
                        await telnet.WriteLineAsync("Goodbye.", hostCancellationToken).ConfigureAwait(false);
                        break;
                    }

                    if (string.Equals(input.Trim(), "train stats", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(input.Trim(), "tat", StringComparison.OrdinalIgnoreCase))
                    {
                        var persisted = _characters.LoadCharacter(session.Actor.Id);
                        if (persisted is null)
                        {
                            await telnet.WriteLineAsync("Character save was not found.", hostCancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        var catalog = _characters.GetCatalog(persisted.WorldId);
                        var training = await RunClassicCharacterCreationScreenAsync(
                            telnet, catalog.RequireRace(persisted.RaceId), catalog.RequireClass(persisted.ClassId),
                            hostCancellationToken, persisted).ConfigureAwait(false);
                        if (training is not null)
                        {
                            var conditions = new Dictionary<string, string>(persisted.Conditions, StringComparer.OrdinalIgnoreCase)
                            {
                                ["given_name"] = persisted.Conditions.TryGetValue("given_name", out var lockedGiven) ? lockedGiven : training.GivenName,
                                ["family_name"] = training.FamilyName,
                                ["hair_length"] = training.HairLength,
                                ["hair_color"] = training.HairColor,
                                ["eye_color"] = training.EyeColor,
                                ["creation_points"] = training.CreationPointsRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                ["cp"] = training.CreationPointsRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            };
                            var givenName = conditions["given_name"];
                            var displayName = string.IsNullOrWhiteSpace(training.FamilyName) ? givenName : $"{givenName} {training.FamilyName}";
                            _characters.SaveCharacter(persisted with
                            {
                                DisplayName = displayName,
                                BaseStats = training.Stats,
                                Conditions = conditions,
                                Sex = training.Sex,
                                UpdatedAtUtc = DateTimeOffset.UtcNow
                            });
                            await telnet.WriteLineAsync("Your character changes have been saved.", hostCancellationToken).ConfigureAwait(false);
                        }
                        continue;
                    }

                    var response = _commands.Dispatch(session, input);
                    if (!string.IsNullOrWhiteSpace(response))
                        await telnet.WriteLineAsync(response, hostCancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                try { _characters.SaveSession(session); }
                catch (Exception saveException) { Console.Error.WriteLine($"Character save failed for {session.Actor.Id}: {saveException.Message}"); }
                _sessions.Remove(session.Id);
            }
        }
        catch (OperationCanceledException)
        {
            // Client login timeout or host shutdown.
        }
        catch (IOException)
        {
            // Remote endpoint disconnected.
        }
        catch (SocketException)
        {
            // Remote endpoint disconnected.
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Telnet session {sessionId} failed: {exception.Message}");
        }
    }

    private async Task<RuntimeAccountRecord?> AuthenticateAccountAsync(TelnetConnection telnet, CancellationToken cancellationToken)
    {
        await telnet.WriteAsync("Account: ", cancellationToken).ConfigureAwait(false);
        var accountName = await telnet.ReadLineAsync(hideInput: false, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accountName))
        {
            await telnet.WriteLineAsync("Connection closed: blank account names are not accepted.", cancellationToken).ConfigureAwait(false);
            return null;
        }

        accountName = accountName.Trim();
        var isLocalHost = IPAddress.IsLoopback(_options.Network.ResolveBindAddress());
        var isSysopTest = isLocalHost && string.Equals(accountName, "SysopTest", StringComparison.OrdinalIgnoreCase);
        var accountExists = _characters.AccountExists(accountName);

        if (!accountExists)
        {
            await telnet.WriteLineAsync(isSysopTest
                ? "Local Sysop test account requested."
                : "Account not found.", cancellationToken).ConfigureAwait(false);

            if (!await ConfirmAsync(telnet, "Create this account? Y/N: ", cancellationToken).ConfigureAwait(false))
            {
                await telnet.WriteLineAsync("Account creation cancelled.", cancellationToken).ConfigureAwait(false);
                return null;
            }
        }

        var account = accountExists ? _characters.TryLoadAccount(accountName)! : _characters.GetOrCreateAccount(accountName);

        if (!_accountSecurity.HasPassword(account.AccountId))
        {
            if (accountExists && !isLocalHost)
            {
                await telnet.WriteLineAsync("This legacy account can only be claimed from the local Host.", cancellationToken).ConfigureAwait(false);
                return null;
            }

            await telnet.WriteLineAsync(accountExists
                ? "This existing account must be claimed with a password."
                : "Creating a new password-protected account.", cancellationToken).ConfigureAwait(false);

            for (var passwordAttempt = 1; passwordAttempt <= 3; passwordAttempt++)
            {
                await telnet.WriteAsync("New password (minimum 8 characters): ", cancellationToken).ConfigureAwait(false);
                var first = await telnet.ReadLineAsync(hideInput: true, cancellationToken).ConfigureAwait(false);
                if (first is null)
                {
                    await telnet.WriteLineAsync("Password was not received. Connection closed.", cancellationToken).ConfigureAwait(false);
                    return null;
                }

                await telnet.WriteAsync("Confirm password: ", cancellationToken).ConfigureAwait(false);
                var second = await telnet.ReadLineAsync(hideInput: true, cancellationToken).ConfigureAwait(false);
                if (second is null)
                {
                    await telnet.WriteLineAsync("Password confirmation was not received. Connection closed.", cancellationToken).ConfigureAwait(false);
                    return null;
                }

                if (!string.Equals(first, second, StringComparison.Ordinal))
                {
                    await telnet.WriteLineAsync(
                        $"Passwords did not match ({passwordAttempt}/3). Please try again.",
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    _accountSecurity.SetPassword(
                        account.AccountId,
                        first,
                        isSysopTest ? RuntimeAccountRole.Sysop : RuntimeAccountRole.Player);
                }
                catch (ArgumentException exception)
                {
                    await telnet.WriteLineAsync(
                        $"{exception.Message} Attempt {passwordAttempt}/3.",
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await telnet.WriteLineAsync(isSysopTest
                    ? "Local Sysop account created. Role: Sysop."
                    : "Account password created.", cancellationToken).ConfigureAwait(false);
                return account;
            }

            await telnet.WriteLineAsync("Account password was not created after 3 attempts. Connection closed.", cancellationToken).ConfigureAwait(false);
            return null;
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await telnet.WriteAsync("Password: ", cancellationToken).ConfigureAwait(false);
            var password = await telnet.ReadLineAsync(hideInput: true, cancellationToken).ConfigureAwait(false);
            var failure = "Password was not received.";
            if (string.IsNullOrEmpty(password))
            {
                await telnet.WriteLineAsync("Blank passwords are not accepted.", cancellationToken).ConfigureAwait(false);
                attempt--;
                continue;
            }

            if (_accountSecurity.VerifyPassword(account.AccountId, password, out failure))
            {
                var credential = _accountSecurity.TryLoad(account.AccountId);
                await telnet.WriteLineAsync($"Login successful. Role: {credential?.Role ?? RuntimeAccountRole.Player}.", cancellationToken).ConfigureAwait(false);
                return account;
            }

            await telnet.WriteLineAsync($"Login failed ({attempt}/3): {failure}", cancellationToken).ConfigureAwait(false);
        }

        await telnet.WriteLineAsync("Too many failed login attempts. Connection closed.", cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static async Task<bool> ConfirmAsync(TelnetConnection telnet, string prompt, CancellationToken cancellationToken)
    {
        while (true)
        {
            await telnet.WriteAsync(prompt, cancellationToken).ConfigureAwait(false);
            var response = await telnet.ReadLineAsync(hideInput: false, cancellationToken).ConfigureAwait(false);
            if (response is null) return false;
            if (string.Equals(response.Trim(), "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(response.Trim(), "yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(response.Trim(), "n", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(response.Trim(), "no", StringComparison.OrdinalIgnoreCase)) return false;
            await telnet.WriteLineAsync("Please enter Y or N.", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteBannerAsync(TelnetConnection telnet, CancellationToken cancellationToken)
    {
        await telnet.WriteLineAsync(_options.HostName, cancellationToken).ConfigureAwait(false);
        await telnet.WriteLineAsync("MajorMUD Reforged - password-protected Telnet login", cancellationToken).ConfigureAwait(false);
        await telnet.WriteLineAsync("Host authoritative. Classic-first. No client-side state mutation.", cancellationToken).ConfigureAwait(false);
        await telnet.WriteLineAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<RuntimeCharacterRecord?> SelectOrCreateCharacterAsync(
        RuntimeAccountRecord account,
        RuntimeWorld world,
        TelnetConnection telnet,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var existing = _characters.ListCharacters(account.AccountId)
                .Where(character => string.Equals(character.WorldId, world.Definition.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (existing.Length > 0)
            {
                await telnet.WriteLineAsync("Characters:", cancellationToken).ConfigureAwait(false);
                for (var index = 0; index < existing.Length; index++)
                {
                    var character = existing[index];
                    var sexLabel = string.IsNullOrWhiteSpace(character.Sex) || string.Equals(character.Sex, "Unspecified", StringComparison.OrdinalIgnoreCase)
                        ? string.Empty
                        : character.Sex + " ";
                    await telnet.WriteLineAsync($"  {index + 1}) {character.DisplayName} [{sexLabel}{character.RaceName} {character.ClassName}, level {character.Level}]", cancellationToken).ConfigureAwait(false);
                }
                var canCreate = existing.Length < _characters.MaximumCharactersPerAccountPerWorld;
                if (canCreate)
                    await telnet.WriteLineAsync($"  {existing.Length + 1}) Create a new character", cancellationToken).ConfigureAwait(false);
                await telnet.WriteAsync(canCreate ? $"Enter 1-{existing.Length + 1}: " : $"Enter 1-{existing.Length}: ", cancellationToken).ConfigureAwait(false);
                var selection = await telnet.ReadLineAsync(hideInput: false, cancellationToken).ConfigureAwait(false);
                if (selection is null) return null;
                if (int.TryParse(selection.Trim(), out var selectedIndex))
                {
                    if (selectedIndex >= 1 && selectedIndex <= existing.Length) return existing[selectedIndex - 1];
                    if (!canCreate || selectedIndex != existing.Length + 1)
                    {
                        await telnet.WriteLineAsync("Invalid character selection.", cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                }
                else
                {
                    await telnet.WriteLineAsync("Invalid character selection.", cancellationToken).ConfigureAwait(false);
                    continue;
                }
            }

            var catalog = _characters.GetCatalog(world.Definition.Id);
            var races = catalog.Races.ToArray();
            IReadOnlyList<string>? raceHelp = null;
            var raceDescriptionPath = Path.Combine(_characters.ProjectRoot, "Data", world.Definition.Id, "race_descriptions.json");
            if (File.Exists(raceDescriptionPath))
            {
                var raceDescriptions = RuntimeClassicDescriptionAuthority.Load(_characters.ProjectRoot, world.Definition.Id);
                raceHelp = races.Select(value => raceDescriptions.TryRaceDescription(value.Name, out var description)
                    ? description
                    : $"No decoded description is available for {value.Name}.").ToArray();
            }
            var race = await SelectNumberedAsync(telnet, "Choose a race:", races.Select(value => value.Name).ToArray(), cancellationToken, raceHelp).ConfigureAwait(false);
            if (race is null) return null;

            var classes = catalog.Classes.ToArray();
            var characterClass = await SelectNumberedAsync(telnet, "Choose a class:", classes.Select(value => value.Name).ToArray(), cancellationToken).ConfigureAwait(false);
            if (characterClass is null) return null;

            var raceDefinition = catalog.RequireRace(race);
            var classDefinition = catalog.RequireClass(characterClass);
            var creation = await RunClassicCharacterCreationScreenAsync(telnet, raceDefinition, classDefinition, cancellationToken, null).ConfigureAwait(false);
            if (creation is null) return null;

            try
            {
                return _characters.CreateCharacter(
                    account, world, creation.DisplayName, creation.Sex, race, characterClass,
                    creation.Stats, creation.GivenName, creation.FamilyName,
                    creation.HairLength, creation.HairColor, creation.EyeColor,
                    creation.CreationPointsRemaining);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                await telnet.WriteLineAsync($"Character creation failed: {exception.Message}", cancellationToken).ConfigureAwait(false);
                await telnet.WriteLineAsync("Please try again; the connection will remain open.", cancellationToken).ConfigureAwait(false);
            }
        }
    }


    private sealed record ClassicCharacterCreationResult(
        string DisplayName,
        string GivenName,
        string FamilyName,
        string Sex,
        IReadOnlyDictionary<string, int> Stats,
        string HairLength,
        string HairColor,
        string EyeColor,
        int CreationPointsRemaining);

    private static async Task<ClassicCharacterCreationResult?> RunClassicCharacterCreationScreenAsync(
        TelnetConnection telnet,
        RuntimeCharacterDefinition race,
        RuntimeCharacterDefinition characterClass,
        CancellationToken cancellationToken,
        RuntimeCharacterRecord? existingCharacter)
    {
        const string Reset = "\u001b[0m";
        const string Cyan = "\u001b[36m";
        const string White = "\u001b[37m";
        const string Magenta = "\u001b[35m";
        const string Red = "\u001b[31m";
        const string Yellow = "\u001b[33m";
        const string DarkGrey = "\u001b[30;1m";
        const string Reverse = "\u001b[7m";
        var trainingMode = existingCharacter is not null;
        var statOrder = new[] { "strength", "intellect", "willpower", "agility", "health", "charm" };
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = "Strength", ["intellect"] = "Intellect", ["willpower"] = "Willpower",
            ["agility"] = "Agility", ["health"] = "Health", ["charm"] = "Charm"
        };

        string StatDataKey(string key) => key.ToLowerInvariant() switch
        {
            "strength" => "str",
            "intellect" => "int",
            "willpower" => "wil",
            "agility" => "agl",
            "health" => "hea",
            "charm" => "cha",
            _ => key
        };

        int ReadStat(IReadOnlyDictionary<string, int> values, string key, int fallback)
        {
            if (values.TryGetValue(key, out var direct)) return direct;
            return values.TryGetValue(StatDataKey(key), out var compact) ? compact : fallback;
        }

        int MinimumFor(string key)
        {
            var raceMin = ReadStat(race.MinimumStats, key, 0);
            var classMin = ReadStat(characterClass.MinimumStats, key, 0);
            return Math.Max(raceMin, classMin);
        }

        int MaximumFor(string key)
        {
            var raceMax = ReadStat(race.MaximumStats, key, 999);
            var classMax = ReadStat(characterClass.MaximumStats, key, 999);
            var combined = Math.Min(raceMax > 0 ? raceMax : 999, classMax > 0 ? classMax : 999);
            return combined == 999 ? Math.Max(MinimumFor(key), 100) : Math.Max(MinimumFor(key), combined);
        }

        var conditions = existingCharacter?.Conditions ?? new Dictionary<string, string>();
        string Condition(string key, string fallback) => conditions.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        var given = trainingMode ? Condition("given_name", existingCharacter!.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? existingCharacter.DisplayName) : string.Empty;
        var family = trainingMode ? Condition("family_name", string.Join(" ", existingCharacter!.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))) : string.Empty;
        var sex = trainingMode ? existingCharacter!.Sex : "Male";
        var hairLengths = new[] { "short", "shoulder-length", "waist-length", "bald" };
        var hairColors = new[] { "black", "brown", "blonde", "red", "silver", "white", "blue" };
        var eyeColors = new[] { "brown", "blue", "green", "hazel", "grey", "pale-blue", "crimson", "violet" };
        int IndexOf(string[] values, string value, int fallback) { var index = Array.FindIndex(values, x => x.Equals(value, StringComparison.OrdinalIgnoreCase)); return index < 0 ? fallback : index; }
        var hairLengthIndex = IndexOf(hairLengths, Condition("hair_length", "short"), 0);
        var hairColorIndex = IndexOf(hairColors, Condition("hair_color", "brown"), 1);
        var eyeColorIndex = IndexOf(eyeColors, Condition("eye_color", "brown"), 0);
        var originalStats = statOrder.ToDictionary(key => key, key => existingCharacter?.BaseStats.TryGetValue(key, out var v) == true ? v : MinimumFor(key), StringComparer.OrdinalIgnoreCase);
        var stats = new Dictionary<string, int>(originalStats, StringComparer.OrdinalIgnoreCase);
        var pool = trainingMode && int.TryParse(Condition("cp", Condition("creation_points", "0")), out var availableCp) ? Math.Max(0, availableCp) : 100;
        var selected = trainingMode ? 2 : 1;
        var exitChoice = "SAVE";
        var message = string.Empty;
        var editBuffer = string.Empty;
        var editing = false;

        static int CostFromBase(int increase)
        {
            var cost = 0;
            for (var point = 1; point <= increase; point++) cost += ((point - 1) / 10) + 1;
            return cost;
        }
        int AdditionalCost(string key, int value)
        {
            var minimum = MinimumFor(key);
            var original = originalStats[key];
            return Math.Max(0, CostFromBase(Math.Max(0, value - minimum)) - CostFromBase(Math.Max(0, original - minimum)));
        }
        int Spent() => statOrder.Sum(key => AdditionalCost(key, stats[key]));
        string FieldText(int field, string committed, int width)
        {
            var shown = selected == field && editing ? editBuffer : committed;
            if (shown.Length > width) shown = shown[..width];
            shown = shown.PadRight(width);
            return selected == field ? $"{Reverse}{shown}{Reset}{White}" : shown;
        }
        string GivenDisplay()
        {
            var value = string.IsNullOrWhiteSpace(given) ? "<not set>" : given;
            if (trainingMode) return $"{DarkGrey}{value.PadRight(18)}{Reset}{White}";
            return FieldText(1, value, 18);
        }

        async Task DrawAsync()
        {
            var remaining = pool - Spent();
            await telnet.WriteAsync("\u001b[?25l\u001b[2J\u001b[H", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White}   .------------------------------------------.---.        .---------------------------.{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White}  / {Cyan}M A J O R  M U D Character Creation{White} /   \\      / {Magenta}Point Cost Chart{White}          \\{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} |                                          |    |   |                           |{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Given Name  {White}{GivenDisplay()} {Red}«{White} |    |   | {Magenta}1st 10 points: 1 CP each{White} |{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Family Name {White}{FieldText(2, string.IsNullOrWhiteSpace(family) ? "<none>" : family, 18)} {Red}«{White} |    |   | {Magenta}2nd 10 points: 2 CP each{White} |{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Race        {White}{race.Name,-18} {Red}«{White} |    |   | {Magenta}3rd 10 points: 3 CP each{White} |{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Class       {White}{characterClass.Name,-18} {Red}«{White} |    |   |       ... and so on ...     |{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} |                                          |    |   |                           |{Reset}", cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < statOrder.Length; i++)
            {
                var key = statOrder[i];
                var field = i + 3;
                var valueText = FieldText(field, stats[key].ToString(), 3);
                await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} {labels[key],-11}{White}( {MinimumFor(key),3} to {MaximumFor(key),3}) {valueText} {Red}«{White} |    |   | {Magenta}+{(i + 1) * 10,2} to base stat: {CostFromBase((i + 1) * 10),3} CP{White} |{Reset}", cancellationToken).ConfigureAwait(false);
            }
            await telnet.WriteLineAsync($"{White} |                                          |    |   |       ... and so on ...     |{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Hair Length {White}{FieldText(9, hairLengths[hairLengthIndex], 18)} {Red}«{White} |    |   '---------------------------'{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Hair Colour {White}{FieldText(10, hairColors[hairColorIndex], 18)} {Red}«{White} |    |    {Cyan}Use the Space Bar to{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Eye Colour  {White}{FieldText(11, eyeColors[eyeColorIndex], 18)} {Red}«{White} |    |    {Cyan}toggle between choices for{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Sex         {White}{FieldText(12, sex, 18)} {Red}«{White} |    |    {Cyan}your physical description{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} |                                          |    |{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} | {Red}»{Cyan} Exit: {White}{FieldText(13, exitChoice, 4)} {Red}«  »{Cyan} CP Left: {White}{remaining,3} {Red}«{White} |    |    SAVE your character or EXIT{Reset}", cancellationToken).ConfigureAwait(false);
            await telnet.WriteLineAsync($"{White} '------------------------------------------'---'{Reset}", cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(message))
                await telnet.WriteLineAsync($"{Yellow}{message}{Reset}", cancellationToken).ConfigureAwait(false);
        }

        bool IsTextField(int field) => field is 1 or 2 || field is >= 3 and <= 8;

        string CurrentFieldValue()
        {
            if (selected == 1) return given;
            if (selected == 2) return family;
            if (selected is >= 3 and <= 8) return stats[statOrder[selected - 3]].ToString();
            return string.Empty;
        }

        void BeginEditing(bool replaceOnFirstCharacter)
        {
            if (!IsTextField(selected) || (trainingMode && selected == 1)) return;
            editBuffer = replaceOnFirstCharacter ? string.Empty : CurrentFieldValue();
            editing = true;
        }

        bool CommitEditing()
        {
            if (!editing) return true;
            var entered = editBuffer.Trim();
            if (selected == 1)
            {
                if (trainingMode) { editing = false; return true; }
                if (string.IsNullOrWhiteSpace(entered)) { message = "Given Name cannot be blank."; return false; }
                given = entered;
            }
            else if (selected == 2)
            {
                family = entered.Equals("<none>", StringComparison.OrdinalIgnoreCase) ? string.Empty : entered;
            }
            else if (selected is >= 3 and <= 8)
            {
                var statKey = statOrder[selected - 3];
                var lower = Math.Max(MinimumFor(statKey), trainingMode ? originalStats[statKey] : MinimumFor(statKey));
                if (!int.TryParse(entered, out var requested)) { message = "Enter a whole-number final stat value."; return false; }
                if (requested < lower || requested > MaximumFor(statKey)) { message = $"{labels[statKey]} must be between {lower} and {MaximumFor(statKey)}."; return false; }
                var old = stats[statKey];
                stats[statKey] = requested;
                if (Spent() > pool)
                {
                    stats[statKey] = old;
                    message = $"You do not have enough CP to set {labels[statKey]} to {requested}.";
                    return false;
                }
                message = $"{labels[statKey]} set to {requested}. {pool - Spent()} CP remain.";
            }
            editing = false;
            editBuffer = string.Empty;
            return true;
        }

        void MoveSelection(int delta)
        {
            if (!CommitEditing()) return;
            do
            {
                selected += delta;
                if (selected < 1) selected = 13;
                if (selected > 13) selected = 1;
            } while (trainingMode && selected == 1);
            message = string.Empty;
        }

        async Task<ClassicCharacterCreationResult?> SaveAsync()
        {
            if (!CommitEditing()) return null;
            if (string.IsNullOrWhiteSpace(given))
            {
                selected = 1;
                message = "You must enter a Given Name before saving.";
                return null;
            }
            try
            {
                RuntimeIdentity.ValidateClassicGivenName(given.Trim());
                if (!string.IsNullOrWhiteSpace(family))
                    RuntimeIdentity.ValidateClassicFamilyName(family.Trim());
            }
            catch (InvalidOperationException exception)
            {
                selected = string.IsNullOrWhiteSpace(given) ? 1 : 2;
                message = exception.Message;
                return null;
            }
            if (Spent() > pool)
            {
                message = "You do not have enough CP for those stat values.";
                return null;
            }
            var display = string.IsNullOrWhiteSpace(family) ? given.Trim() : $"{given.Trim()} {family.Trim()}";
            await telnet.WriteAsync("\u001b[?25h\u001b[0m", cancellationToken).ConfigureAwait(false);
            return new(display, given.Trim(), family.Trim(), sex,
                new Dictionary<string, int>(stats, StringComparer.OrdinalIgnoreCase),
                hairLengths[hairLengthIndex], hairColors[hairColorIndex], eyeColors[eyeColorIndex], pool - Spent());
        }

        while (true)
        {
            await DrawAsync().ConfigureAwait(false);
            var key = await telnet.ReadKeyAsync(cancellationToken).ConfigureAwait(false);
            if (key is null) return null;

            if (key == "UP") { MoveSelection(-1); continue; }
            if (key == "DOWN") { MoveSelection(1); continue; }
            if (key == "ESC") return null;
            if (!editing && string.Equals(key, "s", StringComparison.OrdinalIgnoreCase))
            {
                var saved = await SaveAsync().ConfigureAwait(false);
                if (saved is not null) return saved;
                continue;
            }

            if (key == "BACKSPACE")
            {
                if (!editing) BeginEditing(false);
                if (editing && editBuffer.Length > 0) editBuffer = editBuffer[..^1];
                continue;
            }

            if (key == "SPACE")
            {
                if (editing && IsTextField(selected)) { editBuffer += " "; continue; }
                if (selected == 9) hairLengthIndex = (hairLengthIndex + 1) % hairLengths.Length;
                else if (selected == 10) hairColorIndex = (hairColorIndex + 1) % hairColors.Length;
                else if (selected == 11) eyeColorIndex = (eyeColorIndex + 1) % eyeColors.Length;
                else if (selected == 12) sex = sex.Equals("Male", StringComparison.OrdinalIgnoreCase) ? "Female" : "Male";
                else if (selected == 13) exitChoice = exitChoice == "SAVE" ? "EXIT" : "SAVE";
                continue;
            }

            if (key == "ENTER")
            {
                if (editing)
                {
                    CommitEditing();
                    continue;
                }
                if (selected == 13)
                {
                    if (exitChoice == "EXIT") return null;
                    var saved = await SaveAsync().ConfigureAwait(false);
                    if (saved is not null) return saved;
                    continue;
                }
                if (IsTextField(selected)) { BeginEditing(false); continue; }
                if (selected == 9) hairLengthIndex = (hairLengthIndex + 1) % hairLengths.Length;
                else if (selected == 10) hairColorIndex = (hairColorIndex + 1) % hairColors.Length;
                else if (selected == 11) eyeColorIndex = (eyeColorIndex + 1) % eyeColors.Length;
                else if (selected == 12) sex = sex.Equals("Male", StringComparison.OrdinalIgnoreCase) ? "Female" : "Male";
                continue;
            }

            if (key.Length == 1 && !char.IsControl(key[0]) && IsTextField(selected) && !(trainingMode && selected == 1))
            {
                if (!editing) BeginEditing(true);
                var maxLength = selected switch
                {
                    >= 3 and <= 8 => 3,
                    1 => RuntimeIdentity.ClassicGivenNameMaximumLength,
                    _ => RuntimeIdentity.ClassicFamilyNameMaximumLength
                };
                if (editBuffer.Length < maxLength)
                    editBuffer += key;
                continue;
            }
        }
    }

    private static async Task<string?> SelectNumberedAsync(
        TelnetConnection telnet,
        string heading,
        IReadOnlyList<string> options,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? descriptions = null)
    {
        if (options.Count == 0)
        {
            await telnet.WriteLineAsync("No choices are currently available.", cancellationToken).ConfigureAwait(false);
            return null;
        }

        while (true)
        {
            await telnet.WriteLineAsync(heading, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < options.Count; index++)
                await telnet.WriteLineAsync($"  {index + 1}) {options[index]}", cancellationToken).ConfigureAwait(false);
            var supportsDescriptions = descriptions is not null && descriptions.Count == options.Count;
            await telnet.WriteAsync(supportsDescriptions
                ? $"Enter 1-{options.Count}, or ? <number> for details: "
                : $"Enter 1-{options.Count}: ", cancellationToken).ConfigureAwait(false);
            var response = await telnet.ReadLineAsync(hideInput: false, cancellationToken).ConfigureAwait(false);
            if (response is null) return null;
            var entered = response.Trim();
            if (supportsDescriptions && entered.StartsWith("?", StringComparison.Ordinal))
            {
                var requested = entered[1..].Trim();
                if (int.TryParse(requested, out var descriptionIndex) && descriptionIndex >= 1 && descriptionIndex <= options.Count)
                {
                    await telnet.WriteLineAsync(cancellationToken).ConfigureAwait(false);
                    await telnet.WriteLineAsync($"{options[descriptionIndex - 1]}:", cancellationToken).ConfigureAwait(false);
                    await telnet.WriteLineAsync(descriptions![descriptionIndex - 1], cancellationToken).ConfigureAwait(false);
                    await telnet.WriteLineAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }
            }
            if (int.TryParse(entered, out var selected) && selected >= 1 && selected <= options.Count)
                return options[selected - 1];
            await telnet.WriteLineAsync(supportsDescriptions
                ? $"Invalid selection. Enter 1-{options.Count}, or ? <number> for details."
                : "Invalid selection.", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<RuntimeWorld?> SelectWorldAsync(TelnetConnection telnet, CancellationToken cancellationToken)
    {
        var worlds = _worldProvider();
        if (worlds.Count == 0) return null;

        var menu = new[]
        {
            new { Number = 1, Label = "Classic", Aliases = new[] { "classic" } },
            new { Number = 2, Label = "Custom 1", Aliases = new[] { "custom1", "custom-1", "custom_1" } },
            new { Number = 3, Label = "Custom 2", Aliases = new[] { "custom2", "custom-2", "custom_2" } },
            new { Number = 4, Label = "Reforged", Aliases = new[] { "reforged", "aifromclassic", "ai" } }
        };

        while (true)
        {
            await telnet.WriteLineAsync("Select a world:", cancellationToken).ConfigureAwait(false);
            foreach (var item in menu)
            {
                var available = FindWorld(worlds, item.Aliases) is not null;
                await telnet.WriteLineAsync($"  {item.Number}) {item.Label}{(available ? string.Empty : " [not configured]")}", cancellationToken).ConfigureAwait(false);
            }
            await telnet.WriteAsync("Enter 1-4: ", cancellationToken).ConfigureAwait(false);
            var response = await telnet.ReadLineAsync(hideInput: false, cancellationToken).ConfigureAwait(false);
            if (response is null) return null;
            if (!int.TryParse(response.Trim(), out var selected) || selected < 1 || selected > 4)
            {
                await telnet.WriteLineAsync("Invalid world selection.", cancellationToken).ConfigureAwait(false);
                continue;
            }

            var chosen = menu[selected - 1];
            var world = FindWorld(worlds, chosen.Aliases);
            if (world is not null) return world;
            await telnet.WriteLineAsync($"{chosen.Label} is not configured on this Host yet. Choose another world.", cancellationToken).ConfigureAwait(false);
        }
    }

    private static RuntimeWorld? FindWorld(IReadOnlyDictionary<string, RuntimeWorld> worlds, IReadOnlyList<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (worlds.TryGetValue(alias, out var byId)) return byId;
            var match = worlds.Values.FirstOrDefault(world =>
                string.Equals(world.Definition.Id, alias, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(world.Definition.Name, alias, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(world.Definition.Type, alias, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return null;
    }

}
