using Content.Shared.Administration.Logs;
using Content.Server.Medical.Components;
using Content.Server.Popups;
using Content.Shared.FixedPoint;
using Content.Shared.Sticky.Systems;
using Content.Shared.Sticky;
using Content.Shared.Sticky.Components;
using Robust.Shared.Timing;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Content.Server.Polymorph.Systems;
using Content.Shared.Database;

namespace Content.Server.Medical;

public sealed class MedicalPatchSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly StickySystem _stickySystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainers = default!;
    [Dependency] private readonly ReactiveSystem _reactiveSystem = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] protected readonly ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedicalPatchComponent, EntityUnstuckEvent>(OnUnstuck);
        SubscribeLocalEvent<MedicalPatchComponent, EntityStuckEvent>(OnStuck);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        foreach (var comp in EntityManager.EntityQuery<MedicalPatchComponent>())
        {
            if (_timing.CurTime < comp.NextUpdate)
                continue;
            var uid = comp.Owner;

            if (!TryComp<StickyComponent>(uid, out var stickycomp))
                continue;
            if (stickycomp.StuckTo == null)
                continue;
            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(comp.UpdateTime);

            Cycle(uid, comp);
        }
    }
    public void Cycle(EntityUid uid, MedicalPatchComponent component)
    {
        if (!TryInject(uid, component, component.TransferAmount))
        {
            if (!TryComp<StickyComponent>(uid, out var stickycomp))
            return false;
            //_stickySystem.UnstickFromEntity(uid, uid);
            var attemptEv = new AttemptEntityUnstickEvent(stickycomp.StuckTo, uid);
            RaiseLocalEvent(uid, ref attemptEv);
        }
    }
    public bool TryInject(EntityUid uid, MedicalPatchComponent component, FixedPoint2 transferAmount)
    {
        if (!TryComp<StickyComponent>(uid, out var stickycomp))
            return false;

        if (stickycomp.StuckTo == null)
            return false;
        var target = (EntityUid) stickycomp.StuckTo;

        if (!_solutionContainers.TryGetSolution(uid, component.SolutionName, out var medicalPatchSoln, out var medicalPatchSolution) || medicalPatchSolution.Volume == 0)
        {
            //Solution Empty
            return false;
        }
        if (!_solutionContainers.TryGetInjectableSolution(target, out var targetSoln, out var targetSolution))
        {
            //_popupSystem.PopupEntity(Loc.GetString("Medical Patch cant find a bloodsystem"), target);
            return false;
        }
        var realTransferAmount = FixedPoint2.Min(transferAmount, targetSolution.AvailableVolume);
        if (realTransferAmount <= 0)
        {
            _popupSystem.PopupEntity(Loc.GetString("No room to inject"), target);
            return true;
        }
        var removedSolution = _solutionContainers.SplitSolution(medicalPatchSoln.Value, realTransferAmount);
        if (!targetSolution.CanAddSolution(removedSolution))
            return true;
        _reactiveSystem.DoEntityReaction(target, removedSolution, ReactionMethod.Injection);
        _solutionContainers.TryAddSolution(targetSoln.Value, removedSolution);
        return true;
    }
    public void OnStuck(EntityUid uid, MedicalPatchComponent component, ref EntityStuckEvent args)
    {
        if (!_solutionContainers.TryGetSolution(uid, component.SolutionName, out var medicalPatchSoln, out var medicalPatchSolution))
            return;

        //Logg the Patch stick to.
        _adminLogger.Add(LogType.ForceFeed, $"{EntityManager.ToPrettyString(args.User):user} stuck a patch on  {EntityManager.ToPrettyString(args.Target):target} using {EntityManager.ToPrettyString(uid):using} containing {SharedSolutionContainerSystem.ToPrettyString(medicalPatchSolution):medicalPatchSolution}");

        if (component.InjectAmmountOnAttatch > 0)
        {
            if (!TryInject(uid, component, component.InjectAmmountOnAttatch))
                return;
        }
        if (component.InjectPercentageOnAttatch > 0)
        {
            if (medicalPatchSolution.Volume == 0)
                return;
            if (!TryInject(uid, component, medicalPatchSolution.Volume * (component.InjectPercentageOnAttatch / 100)))
                return;
        }
    }
    public void OnUnstuck(EntityUid uid, MedicalPatchComponent component, ref EntityUnstuckEvent args)
    {
        if (component.SingleUse)
        {
            //_polymorph.PolymorphEntity(uid, "UsedMedicalPatch");
            //why does this not work?
            //maybe i cant polymorph somthing inside its own system.
            QueueDel(uid); //Just delete it for now. TODO:
        }
    }
}
