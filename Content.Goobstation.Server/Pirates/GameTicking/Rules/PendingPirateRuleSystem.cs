// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <aviu00@protonmail.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 amogus <113782077+whateverusername0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
// SPDX-FileCopyrightText: 2025 whateverusername0 <whateveremail>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Dataset;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Goobstation.Server.Pirates.GameTicking.Rules;

public sealed partial class PendingPirateRuleSystem : GameRuleSystem<PendingPirateRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;
    [Dependency] private readonly IPrototypeManager _prot = default!;
    [Dependency] private readonly GameTicker _gt = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;

    private static readonly EntProtoId PirateSpawnRule = "PiratesSpawn";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var eqe = QueryActiveRules();
        while (eqe.MoveNext(out var uid, out _, out var pending, out var gamerule))
        {
            pending.PirateSpawnTimer += frameTime;
            if (pending.PirateSpawnTimer >= pending.PirateSpawnTime)
            {
                // remove spawned order.
                AllEntityQuery<BecomesStationComponent, StationMemberComponent>().MoveNext(out var eqData, out _, out _);
                var station = _station.GetOwningStation(eqData);
                if (!TryComp<StationBankAccountComponent>(station, out var bank))
                    return;
                if (station != null && _cargo.TryGetOrderDatabase(station, out var cargoDb) && pending.Order != null)
                {
                    _cargo.RemoveOrder(station.Value, bank.PrimaryAccount, pending.Order.OrderId, cargoDb);
                }

                SendAnnouncement((uid, pending), AnnouncementType.Arrival);
                _gt.StartGameRule(PirateSpawnRule);
                _gt.EndGameRule(uid, gamerule);
                break;
            }
        }
    }

    protected override void Started(EntityUid uid, PendingPirateRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // get station
        AllEntityQuery<BecomesStationComponent, StationMemberComponent>().MoveNext(out var eqData, out _, out _);
        var station = _station.GetOwningStation(eqData);
        if (station == null) return;

        var announcer = component.LocAnnouncer;

        if (_cargo.TryGetOrderDatabase(station, out var cargoDb))
        {
            var price = 25000;
            if (!TryComp<StationBankAccountComponent>(station, out var bank))
                return;

            var balance = _cargo.GetBalanceFromAccount((station.Value, bank), bank.PrimaryAccount);
            price = _rand.Next((int) (balance * 0.75f), (int) (balance * 1.25f));

            var orderId = CargoSystem.GenerateOrderId(cargoDb) + 1984;

            var name = Loc.GetString($"pirates-ransom-{announcer}-name");
            var reason = Loc.GetString($"pirates-ransom-{announcer}-desc", ("num", price));
            var requester = Loc.GetString($"pirates-announcer-{announcer}");

            var ransom = new CargoOrderData(orderId, component.RansomPrototype, name, price, 1, requester, reason, bank.PrimaryAccount, 30);

            component.Order = ransom;

            _cargo.TryAddOrder(station.Value, bank.PrimaryAccount, ransom, cargoDb);
        }

        SendAnnouncement((uid, component), AnnouncementType.Threat);
    }

    public void SendAnnouncement(Entity<PendingPirateRuleComponent> pprule, AnnouncementType atype)
    {
        var announcer = pprule.Comp.LocAnnouncer;

        if (pprule.Comp.LocAnnouncers != null)
            announcer = _rand.Pick(_prot.Index<DatasetPrototype>(pprule.Comp.LocAnnouncers).Values);

        var type = atype.ToString().ToLower();
        var announcement = Loc.GetString($"pirates-announcement-{announcer}-{type}");

        // announcer at the end because shitcode
        announcer = Loc.GetString($"pirates-announcer-{announcer}");

        _chat.DispatchGlobalAnnouncement(announcement, announcer, colorOverride: Color.Orange);
    }

    public EntityQueryEnumerator<ActiveGameRuleComponent, PendingPirateRuleComponent, GameRuleComponent> GetPendingRules()
        => QueryActiveRules();

    public enum AnnouncementType
    {
        // should match with the localization strings
        Threat, Arrival, Paid, Cancelled, NotEnough
    }
}
