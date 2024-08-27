using Content.Server.Implants.Components;
using Content.Shared.Implants;
using Robust.Shared.Containers;
using Content.Shared.Mindcontrol;
using Content.Server.Mindcontrol;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Hands.Components;

namespace Content.Server.Implants;
public sealed class MindcontrolImplantSystem : EntitySystem
{
    [Dependency] private readonly MindcontrolSystem _mindcontrol = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MindcontrolImplantComponent, EntGotRemovedFromContainerMessage>(OnRemove); //implant gets removed, remove traitor
        SubscribeLocalEvent<MindcontrolImplantComponent, ImplantImplantedEvent>(OnImplant);
        SubscribeLocalEvent<MindcontrolImplantComponent, EntGotInsertedIntoContainerMessage>(OnInsert);
    }
    private void OnImplant(EntityUid uid, MindcontrolImplantComponent component, ImplantImplantedEvent args) //called after implanted ?
    {
        if (component.ImplanterUid != null)
        {
            component.HolderUid = Transform(component.ImplanterUid.Value).ParentUid;
            RemComp<PreventSelfImplantComponent>(component.ImplanterUid.Value);
        }
        if (args.Implanted != null)
            EnsureComp<MindcontrolComponent>(args.Implanted.Value);

        component.ImplanterUid = null;
        if (args.Implanted == null)
            return;
        if (!TryComp<MindcontrolComponent>(args.Implanted.Value, out var implanted))
            return;
        implanted.Master = component.HolderUid;
        _mindcontrol.Start(args.Implanted.Value, implanted);
    }
    private void OnInsert(EntityUid uid, MindcontrolImplantComponent component, EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == "implanter_slot")  //being inserted in a implanter.
        {
            component.ImplanterUid = args.Container.Owner;    //save Implanter uid
            component.HolderUid = null;
            EnsureComp<PreventSelfImplantComponent>(component.ImplanterUid.Value);
        }
    }
    private void OnRemove(EntityUid uid, MindcontrolImplantComponent component, EntGotRemovedFromContainerMessage args)
    {

        if (args.Container.ID == "implant") //when implant is removed
        {
            if (HasComp<MindcontrolComponent>(args.Container.Owner))
                RemComp<MindcontrolComponent>(args.Container.Owner);
        }
    }
}
