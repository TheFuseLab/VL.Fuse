using System.Collections.Generic;
using Fuse.compute;
using VL.Core;

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

        public abstract void UpdateResource(AbstractResource theResource);
    }

    public class BufferResourceBinder : AbstractResourceBinder
    {
        private readonly BufferResource _resource;

        private readonly BufferInput<GpuStruct> _input;
        private readonly BufferGet<GpuStruct> _bufferGet;
        private readonly ShaderNode<int> _index;

        private readonly Dictionary<string, AbstractShaderNode> _bufferedAttributes;

        private NodeContext _context;
        private int _subContextId;
        
        public BufferResourceBinder(NodeContext nodeContext, BufferResource theResource, ShaderNode<int> theIndex)
        {
            _context = nodeContext;
            _subContextId = 0;
            _resource = theResource;
            _index = theIndex;
            var hash = theResource.Buffer.GetHashCode();
            _input = new BufferInput<GpuStruct>(ShaderNodesUtil.GetContext(_context,_subContextId++),theResource.Struct);
            _input.SetInput(theResource.Buffer,true, theResource.Struct);
            _bufferGet = new BufferGet<GpuStruct>(ShaderNodesUtil.GetContext(_context,_subContextId++));
            _bufferGet.SetInputs(_input, theIndex,_resource.Struct);
            _bufferGet.AddInput(_resource.Struct);

            _bufferedAttributes = new Dictionary<string, AbstractShaderNode>();
            
            theResource.Attributes.Values.ForEach(attribute =>
            {
                var declareValue = AbstractCreation.AbstractDeclareValue(ShaderNodesUtil.GetContext(_context,_subContextId++), attribute.ShaderNode);
                var getMember = AbstractCreation.AbstractGetMember(ShaderNodesUtil.GetContext(_context,_subContextId++), _bufferGet, attribute.ShaderNode);

                Attributes[attribute.Name] = attribute.IsBuffered ? getMember : declareValue;

                if (attribute.IsBuffered) _bufferedAttributes[attribute.Name] = getMember;
            });
        }

        public DynamicStruct Struct()
        {
            return _resource.Struct;
        }

        public BufferInput<GpuStruct> BufferInput()
        {
            return _input;
        }
        
        public BufferGet<GpuStruct> BufferGet()
        {
            return _bufferGet;
        }
        
        public ShaderNode<int> Index()
        {
            return _index;
        }

        public override ShaderNode<GpuVoid> WriteAttributes()
        {
            return WriteToIndex(_index);
        }

        public override ShaderNode<GpuVoid> WriteToIndex(ShaderNode<int> theIndex)
        {
            var nodes = new List<ShaderNode<GpuVoid>>();
            foreach (var kv in _bufferedAttributes)
            {
                var assign = new AssignValueToMember<GpuStruct>(ShaderNodesUtil.GetContext(_context,_subContextId++));
                assign.SetInputs(_bufferGet, kv.Key, kv.Value);
                nodes.Add(assign);
            }

            var bufferSet = new BufferSet<GpuStruct>(ShaderNodesUtil.GetContext(_context,_subContextId++));
            bufferSet.SetInputs(_input, theIndex, _bufferGet);
            nodes.Add(bufferSet);

            var group = new Group(ShaderNodesUtil.GetContext(_context,_subContextId++));
            group.SetInput(nodes);
            return group;
        }

        public override void UpdateResource(AbstractResource theResource)
        {
            if (_input != null) _input.Value = (theResource as BufferResource)?.Buffer;
        }

        public BufferGet<GpuStruct> GetElement(ShaderNode<int> theIndex)
        {
            var bufferGet = new BufferGet<GpuStruct>(ShaderNodesUtil.GetContext(_context,_subContextId++));
            bufferGet.SetInputs(_input, theIndex, _resource.Struct);
            return bufferGet;
        }
    }
}