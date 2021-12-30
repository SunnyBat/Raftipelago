using System;

namespace RaftipelagoTypes
{
    public sealed class ActionHandler : MarshalByRefObject
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

    public sealed class SingleArgumentActionHandler<T> : MarshalByRefObject
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

    public sealed class TripleArgumentActionHandler<T, U, V> : MarshalByRefObject
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
}
