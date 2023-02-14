using System;
using System.Runtime.Remoting.Lifetime;

namespace RaftipelagoTypes
{
    public class MarshalByRefObjectWithAggressiveLifetimeService : MarshalByRefObject
    {
        private bool _shouldDispose = true;

        protected internal MarshalByRefObjectWithAggressiveLifetimeService(bool shouldDispose)
        {
            _shouldDispose = shouldDispose;
        }

        public override object InitializeLifetimeService()
        {
            if (_shouldDispose)
            {
                ILease lease = (ILease)base.InitializeLifetimeService();
                if (lease.CurrentState == LeaseState.Initial)
                {
                    lease.InitialLeaseTime = TimeSpan.FromSeconds(10);
                    lease.RenewOnCallTime = TimeSpan.FromSeconds(5);
                }
                return lease;
            }
            else
            {
                return null;
            }
        }
    }

    public sealed class ActionHandler : MarshalByRefObjectWithAggressiveLifetimeService
    {
        private Action _delegate;

        public ActionHandler(Action dlgt) : this(dlgt, true) { }

        public ActionHandler(Action dlgt, bool keepForever) : base(!keepForever)
        {
            _delegate = dlgt;
        }

        public void Invoke()
        {
            _delegate();
        }
    }

    public sealed class SingleArgumentActionHandler<T> : MarshalByRefObjectWithAggressiveLifetimeService
    {
        private Action<T> _delegate;

        public SingleArgumentActionHandler(Action<T> dlgt) : this(dlgt, true) { }

        public SingleArgumentActionHandler(Action<T> dlgt, bool keepForever) : base(!keepForever)
        {
            _delegate = dlgt;
        }

        public void Invoke(T arg1)
        {
            _delegate(arg1);
        }
    }

    public sealed class TripleArgumentActionHandler<T, U, V> : MarshalByRefObjectWithAggressiveLifetimeService
    {
        private Action<T, U, V> _delegate;

        public TripleArgumentActionHandler(Action<T, U, V> dlgt) : this(dlgt, true) { }

        public TripleArgumentActionHandler(Action<T, U, V> dlgt, bool keepForever) : base(!keepForever)
        {
            _delegate = dlgt;
        }

        public void Invoke(T arg1, U arg2, V arg3)
        {
            _delegate(arg1, arg2, arg3);
        }
    }

    public sealed class QuadroupleArgumentActionHandler<T, U, V, W> : MarshalByRefObjectWithAggressiveLifetimeService
    {
        private Action<T, U, V, W> _delegate;

        public QuadroupleArgumentActionHandler(Action<T, U, V, W> dlgt) : this(dlgt, true) { }

        public QuadroupleArgumentActionHandler(Action<T, U, V, W> dlgt, bool keepForever) : base(!keepForever)
        {
            _delegate = dlgt;
        }

        public void Invoke(T arg1, U arg2, V arg3, W arg4)
        {
            _delegate(arg1, arg2, arg3, arg4);
        }
    }
}
