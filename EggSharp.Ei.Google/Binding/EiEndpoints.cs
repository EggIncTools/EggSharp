#nullable enable
using System;

namespace EggSharp.Api;

internal static class EiEndpoints {
    public static readonly EndpointSpec BotFirstContact = new("ei/bot_first_contact", ApiPresets.AndroidProfile, RinfoMode.WithUser, false, false);
    public static readonly EndpointSpec CompleteMission = new("ei_afx/complete_mission", ApiPresets.IosProfile, RinfoMode.None, false, true);
    public static readonly EndpointSpec CoopStatus = new("ei/coop_status", ApiPresets.CoopStatusProfile, RinfoMode.WithUser, false, true);
    public static readonly EndpointSpec CoopStatusBot = new("ei/coop_status_bot", ApiPresets.AndroidProfile, RinfoMode.WithUser, false, true);
    public static readonly EndpointSpec Periodicals = new("ei/get_periodicals", ApiPresets.IosProfile, RinfoMode.WithoutUser, false, true);
    public static readonly EndpointSpec ContractsInfo = new("ei_ctx/get_contracts_info", ApiPresets.IosProfile, RinfoMode.WithUser, true, true);
    public static readonly EndpointSpec ContractPlayerInfo = new("ei_ctx/get_contract_player_info", ApiPresets.AndroidProfile, RinfoMode.WithUser, true, true);
    public static readonly EndpointSpec ContractsArchive = new("ei_ctx/get_contracts_archive", ApiPresets.AndroidProfile, RinfoMode.WithUser, false, true);
    public static readonly EndpointSpec SeasonInfos = new("ei_ctx/get_season_infos_v2", ApiPresets.AndroidProfile, RinfoMode.WithUser, true, true);

    public static EndpointSpec FirstContact(bool signed) =>
        new("ei/first_contact_secure", ApiPresets.AndroidProfile, signed ? RinfoMode.WithUser : RinfoMode.None, true, signed);

    public static EndpointSpec UserSubscription(string userId) =>
        new("ei_srv/subscription_status/" + Uri.EscapeDataString(userId), ApiPresets.IosProfile, RinfoMode.None, false, true);
}
