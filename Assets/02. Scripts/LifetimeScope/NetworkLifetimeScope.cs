using TankAttack.Network;
using TankAttack.Network.Manager;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class NetworkLifetimeScope : LifetimeScope
{
    [SerializeField] private NetworkUIView networkUIView;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<NetworkModel>(Lifetime.Singleton);
        builder.Register<UdpGameClient>(Lifetime.Singleton);
        builder.RegisterComponent(networkUIView);
        builder.RegisterEntryPoint<NetworkPresenter>().AsSelf();
        builder.RegisterEntryPoint<HpBarManager>().AsSelf();
        builder.RegisterEntryPoint<DamageTextManager>().AsSelf();
        builder.RegisterEntryPoint<ItemSpawner>();
    }
}