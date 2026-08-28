using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const ulong RemoteMeleeAnnouncementLifetimeMsec = 2000UL;
    private const ulong RemoteMeleeMaximumScheduleAheadMsec = 1800UL;
    private const ulong RemoteMeleeMaximumHitScheduleAheadMsec = 2700UL;
    private const long RemoteMeleeClientTimingToleranceMsec = 80L;
    private const long RemoteMeleeMaximumTransitSlackMsec = 500L;
    private const ulong RemoteMeleeAuthorizationRetentionMsec = 3000UL;

    private bool TryAcceptRemoteMeleeTarget(
        long peerId,
        int targetId,
        KnifeSkinDefinition definition,
        MeleeAttackDefinition attack,
        int attackIndex,
        int swingSequence,
        long clientHitAtMsec,
        int combatEpoch,
        out ulong executeAt,
        out RemoteMeleeSwingState authorizationState)
    {
        var now = Time.GetTicksMsec();
        executeAt = 0UL;
        authorizationState = null!;
        foreach (var expired in _remoteMeleeSwings
            .Where(pair => now > pair.Value.AdditionalTargetsUntil
                + RemoteMeleeAuthorizationRetentionMsec)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _remoteMeleeSwings.Remove(expired);
        }

        if (!_lastRemoteMeleeSwingByPeer.TryGetValue(peerId, out var announced)
            || announced.Sequence != swingSequence
            || announced.AttackIndex != attackIndex
            || announced.CombatEpoch != combatEpoch
            || !string.Equals(
                announced.DefinitionId,
                definition.Id,
                StringComparison.OrdinalIgnoreCase)
            || !TryScheduleRemoteMeleeHit(
                announced,
                attack,
                clientHitAtMsec,
                now,
                out executeAt))
        {
            return false;
        }
        var key = new RemoteMeleeSwingKey(peerId, swingSequence);
        var createdState = false;
        if (!_remoteMeleeSwings.TryGetValue(key, out var state))
        {
            if (executeAt > now + RemoteMeleeMaximumHitScheduleAheadMsec)
            {
                return false;
            }
            var windowStart = Mathf.Max(0.18f, attack.HitProgress - 0.12f);
            var windowEnd = Mathf.Min(0.78f, attack.HitProgress + 0.16f);
            var additionalWindow = (ulong)Mathf.Clamp(
                (windowEnd - windowStart) * attack.Duration * 1000.0f + 140.0f,
                180.0f,
                520.0f);
            state = new RemoteMeleeSwingState
            {
                DefinitionId = definition.Id,
                AttackIndex = attackIndex,
                MaximumTargets = attack.MaxTargets,
                AcceptedAt = now,
                ExecuteAt = executeAt,
                AdditionalTargetsUntil = (now >= executeAt ? now : executeAt)
                    + additionalWindow
            };
            _remoteMeleeSwings[key] = state;
            createdState = true;
        }
        else
        {
            executeAt = state.ExecuteAt;
        }
        if (!string.Equals(state.DefinitionId, definition.Id, StringComparison.OrdinalIgnoreCase)
            || state.AttackIndex != attackIndex
            || !createdState && now > state.AdditionalTargetsUntil
            || state.TargetIds.Contains(targetId)
            || state.TargetIds.Count >= state.MaximumTargets)
        {
            return false;
        }
        state.TargetIds.Add(targetId);
        authorizationState = state;
        return true;
    }

    private bool RecordRemoteMeleeSwingStart(
        long peerId,
        KnifeSkinDefinition definition,
        int attackIndex,
        int swingSequence,
        long clientStartedAtMsec,
        int combatEpoch)
    {
        var now = Time.GetTicksMsec();
        var attack = MeleeAttackCatalog.AttackFor(definition.Style, attackIndex);
        if (!_lastRemoteMeleeSwingByPeer.TryGetValue(peerId, out var previous))
        {
            if (attackIndex != 0
                || clientStartedAtMsec <= 0
                || !TryScheduleRemoteMeleeStart(
                    peerId,
                    attack,
                    now,
                    now,
                    out var openingEffectiveStart))
            {
                return false;
            }
            _lastRemoteMeleeSwingByPeer[peerId] = new RemoteMeleePeerSwing(
                swingSequence,
                definition.Id,
                attackIndex,
                clientStartedAtMsec,
                combatEpoch,
                now,
                openingEffectiveStart,
                attack.Duration,
                attack.HitProgress);
            return true;
        }
        var expectedSequence = previous.Sequence == int.MaxValue
            ? 1
            : previous.Sequence + 1;
        var exactSequence = swingSequence == expectedSequence;
        if ((!exactSequence && swingSequence <= expectedSequence)
            || clientStartedAtMsec <= previous.ClientStartedAtMsec)
        {
            return false;
        }
        var clientDelta = clientStartedAtMsec - previous.ClientStartedAtMsec;
        var expectedCombo = (previous.AttackIndex + 1)
            % MeleeAttackCatalog.AttackCount(definition.Style);
        var sameDefinition = string.Equals(
            previous.DefinitionId,
            definition.Id,
            StringComparison.OrdinalIgnoreCase);
        var comboMinimum = (long)(previous.Duration * 1000.0f)
            - RemoteMeleeClientTimingToleranceMsec;
        var comboMaximum = (long)((previous.Duration
                + MeleeAttackCatalog.ComboWindowDuration) * 1000.0f)
            + RemoteMeleeClientTimingToleranceMsec;
        var restartMinimum = (long)(previous.Duration * 880.0f)
            - RemoteMeleeClientTimingToleranceMsec;
        var validCombo = exactSequence
            && sameDefinition
            && attackIndex == expectedCombo
            && clientDelta >= comboMinimum
            && clientDelta <= comboMaximum;
        var validRestart = attackIndex == 0 && clientDelta >= restartMinimum;
        var authoredDurationMsec = (long)(previous.Duration * 1000.0f);
        var requiresAuthoredComboFloor = validCombo
            && (attackIndex != 0 || clientDelta + 10L >= authoredDurationMsec);
        var authoredComboFloor = requiresAuthoredComboFloor
            ? previous.HostEffectiveStartedAt + (ulong)(previous.Duration * 1000.0f)
            : now;
        if ((!validCombo && !validRestart)
            || !TryScheduleRemoteMeleeStart(
                peerId,
                attack,
                now,
                authoredComboFloor,
                out var effectiveStart))
        {
            return false;
        }
        _lastRemoteMeleeSwingByPeer[peerId] = new RemoteMeleePeerSwing(
            swingSequence,
            definition.Id,
            attackIndex,
            clientStartedAtMsec,
            combatEpoch,
            now,
            effectiveStart,
            attack.Duration,
            attack.HitProgress);
        return true;
    }

    private bool TryScheduleRemoteMeleeStart(
        long peerId,
        MeleeAttackDefinition attack,
        ulong now,
        ulong minimumEffectiveStart,
        out ulong effectiveStart)
    {
        var cost = (ulong)Mathf.Max(400.0f, attack.Duration * 880.0f);
        effectiveStart = _remoteMeleeNextStartByPeer.TryGetValue(peerId, out var nextStart)
            && nextStart > now
            ? nextStart
            : now;
        if (minimumEffectiveStart > effectiveStart)
        {
            effectiveStart = minimumEffectiveStart;
        }
        if (effectiveStart - now > RemoteMeleeMaximumScheduleAheadMsec)
        {
            return false;
        }
        _remoteMeleeNextStartByPeer[peerId] = effectiveStart + cost;
        return true;
    }

    private static bool TryScheduleRemoteMeleeHit(
        RemoteMeleePeerSwing announced,
        MeleeAttackDefinition attack,
        long clientHitAtMsec,
        ulong now,
        out ulong executeAt)
    {
        executeAt = 0UL;
        if (now < announced.HostReceivedAt
            || now - announced.HostReceivedAt > RemoteMeleeAnnouncementLifetimeMsec
            || clientHitAtMsec <= announced.ClientStartedAtMsec)
        {
            return false;
        }
        var windowStart = Mathf.Max(0.18f, attack.HitProgress - 0.12f);
        var windowEnd = Mathf.Min(0.78f, attack.HitProgress + 0.16f);
        var clientElapsed = clientHitAtMsec - announced.ClientStartedAtMsec;
        var acceptedEarliest = (long)Mathf.Max(
            0.0f,
            attack.Duration * windowStart * 1000.0f
                - RemoteMeleeClientTimingToleranceMsec);
        var acceptedLatest = (long)(attack.Duration * windowEnd * 1000.0f)
            + RemoteMeleeClientTimingToleranceMsec;
        if (clientElapsed < acceptedEarliest || clientElapsed > acceptedLatest)
        {
            return false;
        }
        var authoredEarliest = (long)(attack.Duration * windowStart * 1000.0f);
        var authoredLatest = (long)(attack.Duration * windowEnd * 1000.0f);
        var scheduledElapsed = Math.Clamp(
            clientElapsed,
            authoredEarliest,
            authoredLatest);
        executeAt = announced.HostEffectiveStartedAt + (ulong)scheduledElapsed;
        return now <= executeAt + (ulong)RemoteMeleeMaximumTransitSlackMsec;
    }

    internal bool ValidateRemoteMeleeAuthorityForDiagnostics()
    {
        const long cadencePeer = 910001L;
        const long timingPeer = 910002L;
        const long firstIndexPeer = 910003L;
        const long clientStart = 100000L;
        var zhanma = KnifeSkinCatalog.Definition("knife_zhanma");
        var zhanmaOpening = MeleeAttackCatalog.AttackFor(zhanma.Style, 0);
        ForgetRemoteMeleePeer(cadencePeer);
        ForgetRemoteMeleePeer(timingPeer);
        ForgetRemoteMeleePeer(firstIndexPeer);

        try
        {
            var openingAccepted = RecordRemoteMeleeSwingStart(
                cadencePeer,
                zhanma,
                0,
                1,
                clientStart,
                0);
            var openingAnnouncement = _lastRemoteMeleeSwingByPeer[cadencePeer];
            var compressedComboAccepted = RecordRemoteMeleeSwingStart(
                cadencePeer,
                zhanma,
                1,
                2,
                clientStart + 900L,
                0);
            var comboAnnouncement = _lastRemoteMeleeSwingByPeer[cadencePeer];
            var compressedComboScheduled = compressedComboAccepted
                && comboAnnouncement.HostEffectiveStartedAt
                    > openingAnnouncement.HostEffectiveStartedAt;
            var forwardGapRestartAccepted = RecordRemoteMeleeSwingStart(
                cadencePeer,
                zhanma,
                0,
                4,
                clientStart + 1800L,
                0);
            var firstFinisherRejected = !RecordRemoteMeleeSwingStart(
                firstIndexPeer,
                zhanma,
                2,
                1,
                clientStart,
                0);

            var timingStartAccepted = RecordRemoteMeleeSwingStart(
                timingPeer,
                zhanma,
                0,
                1,
                clientStart,
                0);
            var clientHitAt = clientStart
                + (long)(zhanmaOpening.Duration * zhanmaOpening.HitProgress * 1000.0f);
            var earlyHitAccepted = TryAcceptRemoteMeleeTarget(
                timingPeer,
                1,
                zhanma,
                zhanmaOpening,
                0,
                1,
                clientHitAt,
                0,
                out var firstExecuteAt,
                out var firstAuthorizationState);
            var earlyHitScheduled = earlyHitAccepted
                && firstExecuteAt > Time.GetTicksMsec();
            var secondStartAccepted = RecordRemoteMeleeSwingStart(
                timingPeer,
                zhanma,
                1,
                2,
                clientStart + 900L,
                0);
            var secondAnnouncement = _lastRemoteMeleeSwingByPeer[timingPeer];
            var zhanmaReturn = MeleeAttackCatalog.AttackFor(zhanma.Style, 1);
            var secondClientHitAt = clientStart
                + 900L
                + (long)(zhanmaReturn.Duration * zhanmaReturn.HitProgress * 1000.0f);
            var secondHitAccepted = TryAcceptRemoteMeleeTarget(
                timingPeer,
                2,
                zhanma,
                zhanmaReturn,
                1,
                2,
                secondClientHitAt,
                0,
                out var secondExecuteAt,
                out _);
            var returnWindowStart = Mathf.Max(0.18f, zhanmaReturn.HitProgress - 0.12f);
            var authoredDamageSchedule = secondStartAccepted
                && secondHitAccepted
                && secondExecuteAt >= secondAnnouncement.HostEffectiveStartedAt
                    + (ulong)(zhanmaReturn.Duration * returnWindowStart * 1000.0f);
            _remoteMeleeDefinitionByPeer[timingPeer] = zhanma.Id;
            OnRemoteMeleeLoadout(timingPeer, "knife_tianxuan");
            var acceptedHitSurvivesLaterLoadout = firstAuthorizationState is not null
                && PendingRemoteMeleeHitAuthorized(new PendingRemoteMeleeHit(
                    timingPeer,
                    Vector3.Zero,
                    Vector3.Zero,
                    1,
                    zhanma.Id,
                    0,
                    1,
                    0,
                    firstExecuteAt,
                    firstAuthorizationState))
                && !_lastRemoteMeleeSwingByPeer.ContainsKey(timingPeer)
                && string.Equals(
                    _remoteMeleeDefinitionByPeer[timingPeer],
                    "knife_tianxuan",
                    System.StringComparison.OrdinalIgnoreCase);
            ClearRemoteMeleeTransientState(timingPeer);
            var clearedAnnouncementRejected = !TryAcceptRemoteMeleeTarget(
                timingPeer,
                2,
                zhanma,
                zhanmaOpening,
                0,
                1,
                clientHitAt,
                0,
                out _,
                out _);
            return openingAccepted
                && compressedComboScheduled
                && forwardGapRestartAccepted
                && firstFinisherRejected
                && timingStartAccepted
                && earlyHitScheduled
                && authoredDamageSchedule
                && acceptedHitSurvivesLaterLoadout
                && clearedAnnouncementRejected;
        }
        finally
        {
            ForgetRemoteMeleePeer(cadencePeer);
            ForgetRemoteMeleePeer(timingPeer);
            ForgetRemoteMeleePeer(firstIndexPeer);
        }
    }

    private static bool RemoteMeleeActorActive(Node3D actor)
        => actor switch
        {
            TacticalPlayer player => !player.IsDead,
            SquadMate mate => !mate.IsDowned && !mate.IsBodyBag,
            EnemyOperator enemy => !enemy.IsDead,
            _ => false
        };

    private float HostMeleeDamage(
        KnifeSkinDefinition definition,
        MeleeAttackDefinition attack,
        Node3D shooter,
        Node3D target,
        bool allowBackstab)
    {
        var damage = definition.BaseDamage
            * attack.DamageMultiplier
            * _rng.RandfRange(0.92f, 1.08f);
        var toAttacker = MeleeAttackerPosition(shooter) - MeleeTargetPosition(target);
        var facing = MeleeTargetForward(target);
        toAttacker.Y = 0.0f;
        facing.Y = 0.0f;
        var backstab = allowBackstab
            && facing.LengthSquared() > 0.01f
            && toAttacker.LengthSquared() > 0.01f
            && facing.Normalized().Dot(toAttacker.Normalized()) < -0.35f;
        return damage * (backstab ? 1.6f : 1.0f);
    }

    private void ClearRemoteMeleeHitState(long peerId)
    {
        foreach (var key in _remoteMeleeSwings.Keys
            .Where(key => key.PeerId == peerId)
            .ToArray())
        {
            _remoteMeleeSwings.Remove(key);
        }
    }

    private void ClearRemoteMeleeTransientState(long peerId)
    {
        _lastRemoteMeleeSwingByPeer.Remove(peerId);
        ClearRemoteMeleeHitState(peerId);
    }

    private void ClearAllRemoteMeleeTransientState()
    {
        _lastRemoteMeleeSwingByPeer.Clear();
        _remoteMeleeSwings.Clear();
    }

    private void ForgetRemoteMeleePeer(long peerId)
    {
        ClearRemoteMeleeTransientState(peerId);
        _remoteMeleeNextStartByPeer.Remove(peerId);
        _remoteMeleeDefinitionByPeer.Remove(peerId);
    }
}
