using System.Collections.Generic;
using Fuse.compute;
using Stride.Graphics;

namespace Fuse.ComputeSystem
{
    public abstract class AbstractResource
    {
        public Dictionary<string, IAttribute> Attributes { get; private set; }

        protected List<IResourceListener> Listeners;

        public string Name { get; private set; }

        public AbstractResource(string theName)
        {
            Attributes = new Dictionary<string, IAttribute>();

            Listeners = new List<IResourceListener>();

            Name = theName;
        }
        
        protected virtual void OnChangeAttributes(){}

        public void AddAttribute(IAttribute theAttribute)
        {
            if (Attributes.ContainsKey(theAttribute.Name)) return;
            Attributes[theAttribute.Name] = theAttribute;
            OnChangeAttributes();
        }

        public void Reset()
        {
            Attributes.Clear();
        }

        public void AddListener(IResourceListener theListener)
        {
            Listeners.Add(theListener);
        }

        public abstract void CreateResources();
    }

    public class BufferResource : AbstractResource
    {

        private readonly int _elementCount;

        private readonly IBufferCreator _bufferCreator;

        public BufferResource(string theName, int theElementCount, IBufferCreator theBufferCreator = null) : base(theName)
        {
            _elementCount = theElementCount;
            _bufferCreator = theBufferCreator;
        }

        private bool _changedAttributes = false;

        protected override void OnChangeAttributes()
        {
            _changedAttributes = true;
        }

        public override void CreateResources()
        {
            if (!_changedAttributes && Struct != null) return;

            var fields = new List<AbstractShaderNode>();
            Attributes.Values.ForEach(field =>
            {
                if (field == null) return;

                fields.Add(field.ShaderNode);
            });
            Struct = new DynamicStruct(fields, Name);
            if (_bufferCreator != null)
            {
                Buffer = _bufferCreator.CreateBuffer(_elementCount, Struct.Stride);
            }
            //const BufferFlags bufferFlags = BufferFlags.ShaderResource | BufferFlags.UnorderedAccess | BufferFlags.StructuredBuffer;

           //  = Buffer.New(null, _elementCount * Struct.Stride, Struct.Stride, bufferFlags);
        }

        public Buffer Buffer { get; private set; }

        public DynamicStruct Struct { get; private set; } = null;
    }
}