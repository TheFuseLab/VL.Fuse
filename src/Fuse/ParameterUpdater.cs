using Stride.Rendering;
using Stride.Rendering.Materials;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using Stride.Core;

namespace Fuse
{
    /// <summary>
    /// Helper class to easily track parameter collections and update one of its parameters.
    /// </summary>
    /// <typeparam name="TValue">The type of the parameter value</typeparam>
    /// <typeparam name="TKey">The type of the parameter key</typeparam>
    public abstract class ParameterUpdater<TValue, TKey>
        where TKey : ParameterKey
    {
        private static readonly EqualityComparer<TValue> Comparer = EqualityComparer<TValue>.Default;

        // In most of the cases the parameter collection is known from the start and no other will come into play (pin and effect are in the same node)
        private readonly ParameterCollection _parameters;

        // In case we end up in a shader graph multiple parameter collections could pop up (one for every effect) we need to keep track of
        private Dictionary<(ParameterCollection, TKey), RefCountDisposable> _trackedCollections;

        private TValue _value;
        private readonly TKey _key;

        protected ParameterUpdater(ParameterCollection parameters = default, TKey key = default)
        {
            _parameters = parameters;
            _key = key;
        }

        public TValue Value
        {
            get => _value;
            set
            {
                if (Comparer.Equals(value, Value)) return;
                _value = value;

                if (_parameters != null)
                {
                    Upload(_parameters, _key, ref value);
                }

                if (_trackedCollections == null) return;
                
                foreach (var (parameters, key) in _trackedCollections.Keys)
                    Upload(parameters, key, ref value);
            }
        }

        public ImmutableArray<ParameterCollection> GetTrackedCollections()
        {
            if (_trackedCollections is null)
                return ImmutableArray<ParameterCollection>.Empty;

            var result = ImmutableArray.CreateBuilder<ParameterCollection>(_trackedCollections.Count);
            foreach (var (parameters, _) in _trackedCollections.Keys)
                result.Add(parameters);
            return result.ToImmutable();
        }

        public void Track(ShaderGeneratorContext context)
        {
            Track(context, _key);
        }

        private static bool TryGetSubscriptions(ComponentBase context, out CompositeDisposable subscriptions)
        {
            foreach (var key in context.Tags.Keys)
            {
                if (!key.Name.Equals("GraphSubscriptions")) continue;
                subscriptions = context.Tags.Get(key) as CompositeDisposable;
                return true;
            }
            subscriptions = default;
            return false;//context.Tags.TryGetValue(GraphSubscriptions, out subscriptions);
        }

        private void Track(ShaderGeneratorContext context, TKey key)
        {
            if (TryGetSubscriptions(context,out var s))
                s.Add(Subscribe(context.Parameters, key));
        }

        private IDisposable Subscribe(ParameterCollection parameters, TKey key)
        {
            var x = (parameters, key);

            var trackedCollections = this._trackedCollections ??= new Dictionary<(ParameterCollection, TKey), RefCountDisposable>();
            if (trackedCollections.TryGetValue(x, out var disposable))
                return disposable.GetDisposable();

            disposable = new RefCountDisposable(Disposable.Create(() => trackedCollections.Remove(x)));
            trackedCollections.Add(x, disposable);
            Upload(parameters, key, ref _value);
            return disposable;
        }

        protected abstract void Upload(ParameterCollection parameters, TKey key, ref TValue value);
    }

    public sealed class ValueParameterUpdater<T> : ParameterUpdater<T, ValueParameterKey<T>>
        where T : struct
    {
        public ValueParameterUpdater(ParameterCollection parameters = null, ValueParameterKey<T> key = null) : base(parameters, key)
        {

        }

        protected override void Upload(ParameterCollection parameters, ValueParameterKey<T> key, ref T value)
        {
            parameters.Set(key, ref value);
        }
    }

    public sealed class ArrayParameterUpdater<T> : ParameterUpdater<T[], ValueParameterKey<T>>
        where T : struct
    {
        public ArrayParameterUpdater(ParameterCollection parameters = null, ValueParameterKey<T> key = null) : base(parameters, key)
        {

        }

        protected override void Upload(ParameterCollection parameters, ValueParameterKey<T> key, ref T[] value)
        {
            if (value.Length > 0)
                parameters.Set(key, value);
        }
    }

    public sealed class ObjectParameterUpdater<T> : ParameterUpdater<T, ObjectParameterKey<T>>
        where T : class
    {
        public ObjectParameterUpdater(ParameterCollection parameters = null, ObjectParameterKey<T> key = null) : base(parameters, key)
        {

        }

        protected override void Upload(ParameterCollection parameters, ObjectParameterKey<T> key, ref T value)
        {
            parameters.Set(key, value);
        }
    }

    public sealed class PermutationParameterUpdater<T> : ParameterUpdater<T, PermutationParameterKey<T>>
    {
        public PermutationParameterUpdater(ParameterCollection parameters = null, PermutationParameterKey<T> key = null) : base(parameters, key)
        {

        }

        protected override void Upload(ParameterCollection parameters, PermutationParameterKey<T> key, ref T value)
        {
            parameters.Set(key, value);
        }
    }
}