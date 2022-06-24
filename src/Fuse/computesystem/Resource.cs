using System.Collections.Generic;
using Fuse.compute;
using Stride.Graphics;

namespace Fuse.ComputeSystem
{
    public abstract class AbstractResource
    {
        public Dictionary<string, IMember> Attributes { get; private set; }

        protected List<IResourceListener> Listeners;

        public string Name { get; private set; }

        public AbstractResource(string theName)
        {
            Attributes = new Dictionary<string, IMember>();

            Listeners = new List<IResourceListener>();

            Name = theName;
        }
        
        protected virtual void OnChangeAttributes(){}

        public void AddAttribute(IMember theMember)
        {
            if (Attributes.ContainsKey(theMember.Name)) return;
            Attributes[theMember.Name] = theMember;
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

        public BufferResource(string theName, int theElementCount) : base(theName)
        {
            _elementCount = theElementCount;
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

            const BufferFlags bufferFlags = BufferFlags.ShaderResource | BufferFlags.UnorderedAccess | BufferFlags.StructuredBuffer;

           // Buffer = Buffer.New(null, _elementCount * Struct.Stride, Struct.Stride, bufferFlags);
        }

        public Buffer Buffer { get; private set; }

        public DynamicStruct Struct { get; private set; } = null;
    }
}