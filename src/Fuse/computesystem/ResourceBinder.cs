using System.Collections.Generic;
using Fuse.compute;
using NUnit.Framework;

namespace Fuse.ComputeSystem
{
    public abstract class AbstractResourceBinder
    {
        
        protected readonly Dictionary<string, AbstractShaderNode> Attributes;

        protected AbstractResourceBinder()
        {
            Attributes = new Dictionary<string, AbstractShaderNode>();
        }
        public abstract ShaderNode<GpuVoid> WriteAttributes();

        public AbstractShaderNode GetAttribute(string theName)
        {
            return Attributes[theName];
        }

        public abstract ShaderNode<GpuVoid> WriteToIndex(ShaderNode<int> theIndex);
    }

    public class BufferResourceBinder : AbstractResourceBinder
    {
        private readonly BufferResource _resource;

        private readonly BufferInput<GpuStruct> _input;
        private readonly DynamicStructBufferGet _bufferGet;
        private readonly ShaderNode<int> _index;

        private readonly Dictionary<string, AbstractShaderNode> _bufferedAttributes;
        
        public BufferResourceBinder(BufferResource theResource, ShaderNode<int> theIndex)
        {
            _resource = theResource;
            _index = theIndex;
            
            _input = new BufferInput<GpuStruct>(theResource.Struct, theResource.Buffer);
            _bufferGet = new DynamicStructBufferGet(_input, theIndex, _resource.Struct, null);

            _bufferedAttributes = new Dictionary<string, AbstractShaderNode>();
            
            theResource.Attributes.Values.ForEach(attribute =>
            {
                var declareValue = AbstractCreation.AbstractDeclareValue(attribute.ShaderNode);
                var getMember = AbstractCreation.AbstractGetMember(_bufferGet, attribute.ShaderNode);

                Attributes[attribute.Name] = attribute.IsBuffered ? getMember : declareValue;

                if (attribute.IsBuffered) _bufferedAttributes[attribute.Name] = getMember;
            });
        }

        public override ShaderNode<GpuVoid> WriteAttributes()
        {
            return WriteToIndex(_index);
        }

        public override ShaderNode<GpuVoid> WriteToIndex(ShaderNode<int> theIndex)
        {
            var nodes = new List<ShaderNode<GpuVoid>>();
            _bufferedAttributes.ForEach(kv =>
            {
                nodes.Add(new DynamicStructSetAbstractAttribute(_bufferGet, kv.Key, kv.Value));
            });
            nodes.Add(new DynamicStructBufferSet(_input, theIndex, _bufferGet));

            return new Group(nodes);
        }

        public void UpdateResource(BufferResource theResource)
        {
            if (_input != null) _input.Value = theResource.Buffer;
        }

        public DynamicStructBufferGet GetElement(ShaderNode<int> theIndex)
        {
            return new DynamicStructBufferGet(_input, theIndex, _resource.Struct, null);
        }
    }
}