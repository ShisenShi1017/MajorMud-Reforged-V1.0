using System;
using System.Collections.Generic;

namespace RealmGate.Server;

public enum NativeSupportDistribution { Wcc, Wbb }

public static class NativeSupportDistributionCoverageRegistry
{
    public const string EvidenceId = "native-support-distribution-coverage-0026";

    private static readonly HashSet<int> WccHouseDescriptionIds = new() { 850, 851, 852, 857, 861, 862, 863, 868, 874, 875, 876, 880, 881, 882, 883, 885, 887, 889, 890, 891, 892, 896, 902, 903, 904, 908, 909, 911, 919, 921, 922, 923, 924, 931, 932, 933, 934, 940, 942, 946, 947, 948, 949, 960, 961, 968, 971 };
    private static readonly HashSet<int> WbbHouseDescriptionIds = new() { 850, 851, 852, 853, 854, 855, 856, 857, 858, 859, 860, 861, 862, 863, 864, 865, 866, 867, 868, 869, 870, 871, 872, 873, 874, 875, 876, 877, 878, 879, 880, 881, 882, 883, 884, 885, 886, 887, 888, 889, 890, 891, 892, 893, 894, 895, 896, 897, 898, 899, 900, 901, 902, 903, 904, 905, 906, 907, 908, 909, 910, 911, 912, 913, 914, 915, 916, 917, 918, 919, 920, 921, 922, 923, 924, 925, 926, 927, 928, 929, 930, 931, 932, 933, 934, 935, 936, 937, 938, 939, 940, 941, 942, 943, 944, 945, 946, 947, 948, 949, 950, 951, 952, 953, 954, 955, 956, 957, 958, 959, 960, 961, 962, 963, 964, 965, 966, 967, 968, 969, 970, 971, 972, 973, 974, 975, 976, 977, 978, 979, 980, 981, 982 };

    public static bool ShipsHouseDescription(NativeSupportDistribution distribution, int houseId) =>
        distribution switch
        {
            NativeSupportDistribution.Wcc => WccHouseDescriptionIds.Contains(houseId),
            NativeSupportDistribution.Wbb => WbbHouseDescriptionIds.Contains(houseId),
            _ => false
        };

    public static bool CanUseCrossDistributionFallback(NativeSupportDistribution requested, NativeSupportDistribution source) =>
        requested == source;

    public static void Validate()
    {
        if (WccHouseDescriptionIds.Count != 47) throw new InvalidOperationException("WCC HSE coverage mismatch.");
        if (WbbHouseDescriptionIds.Count != 133) throw new InvalidOperationException("WBB HSE coverage mismatch.");
        if (!WccHouseDescriptionIds.IsSubsetOf(WbbHouseDescriptionIds)) throw new InvalidOperationException("WCC must be a subset of WBB for supplied HSE evidence.");
    }
}
