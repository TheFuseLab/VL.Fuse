using System.Collections.Generic;
using Fuse.compute;
using Stride.Graphics;
using VL.Core;

namespace Fuse.ComputeSystem
{
    public abstract class AbstractResource
    {
        public Dictionary<string, IAttribute> Attributes { get; private set; }

        protected readonly List<IResourceListener> Listeners;

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

        public List<string> GetAttributeDescriptions()
        {
            foreach (var attribute in Attributes.Values)
            {
                TypeHelpers.GetDescription(attribute.ShaderNode);
            }
            
            return null;
        }

        public abstract void Update();
    }

    public class BufferResource : AbstractResource
    {

        private readonly int _elementCount;

        private readonly IBufferCreator _bufferCreator;
        
        private NodeSubContextFactory _subContextFactory;

        public BufferResource(NodeContext nodeContext, string theName, int theElementCount, IBufferCreator theBufferCreator = null) : base(theName)
        {
            _elementCount = theElementCount;
            _bufferCreator = theBufferCreator;
            _subContextFactory = new NodeSubContextFactory(nodeContext);
        }

        private bool _changedAttributes = false;

        protected override void OnChangeAttributes()
        {
            _changedAttributes = true;
        }

        public override void Update()
        {
            if (!_changedAttributes && Struct != null) return;

            var fields = new List<AbstractShaderNode>();
            Attributes.Values.ForEach(field =>
            {
                if (field == null) return;

                fields.Add(field.ShaderNode);
            });
            Struct = new DynamicStruct(_subContextFactory.NextSubContext());
            Struct.SetInput(fields, Name);
            if (_bufferCreator == null) return;
            
            Buffer?.Dispose();
            Buffer = _bufferCreator.CreateBuffer(_elementCount, Struct.Stride);
        }

        public Buffer Buffer { get; private set; }

        public DynamicStruct Struct { get; private set; } = null;
    }
}