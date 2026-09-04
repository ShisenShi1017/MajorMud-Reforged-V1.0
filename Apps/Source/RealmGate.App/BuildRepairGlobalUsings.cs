// Pass 8 build repair: the external-player session lifecycle uses FileInfo directly.
// Keep this as a global import so the repair does not replace or otherwise alter player behavior.
global using System.IO;
