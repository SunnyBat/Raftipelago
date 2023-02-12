using System;
using System.Runtime.Remoting.Lifetime;

namespace RaftipelagoTypes
{
    public class MarshalByRefObjectWithAggressiveLifetimeService : MarshalByRefObject
    {
        public override object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromSeconds(4);
                lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }
    }

    public sealed class ActionHandler : MarshalByRefObjectWithAggressiveLifetimeService
    {
        public void Invoke()
        {
            _delegate();
        }

        private Action _delegate;

        public ActionHandler(Action dlgt)
        {
            _delegate = dlgt;
        }
    }

    public sealed class SingleArgumentActionHandler<T> : MarshalByRefObjectWithAggressiveLifetimeService
    {
        public void Invoke(T arg1)
        {
            _delegate(arg1);
        }

        private Action<T> _delegate;

        public SingleArgumentActionHandler(Action<T> dlgt)
        {
            _delegate = dlgt;
        }
    }

    public sealed class TripleArgumentActionHandler<T, U, V> : MarshalByRefObjectWithAggressiveLifetimeService
    {
        public void Invoke(T arg1, U arg2, V arg3)
        {
            _delegate(arg1, arg2, arg3);
        }

        private Action<T, U, V> _delegate;

        public TripleArgumentActionHandler(Action<T, U, V> dlgt)
        {
            _delegate = dlgt;
        }
    }

    public sealed class QuadroupleArgumentActionHandler<T, U, V, W> : MarshalByRefObjectWithAggressiveLifetimeService
    {
        public void Invoke(T arg1, U arg2, V arg3, W arg4)
        {
            _delegate(arg1, arg2, arg3, arg4);
        }

        private Action<T, U, V, W> _delegate;

        public QuadroupleArgumentActionHandler(Action<T, U, V, W> dlgt)
        {
            _delegate = dlgt;
        }
    }
}
