using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.ComputeSystem
{
    public interface IResourceHandler
    {
        public AbstractResource CreateResource(string name);

        public void GetThreadGroupInfo(out Int3 threadGroupCount, out Int3 threadGroupSize);

        public AbstractResourceBinder CreateBinder(AbstractResource resource, ShaderNode<Int3> index);

        public int Ticket { get; }
    }

    public interface IBufferCreator
    {
        public Buffer CreateBuffer(int elementCount, int stride);
    }
    
    public class BufferResourceHandler : IResourceHandler
    {
        private int _elementCount;

        private Dictionary<string, BufferResource> _resources;

        private readonly IBufferCreator _bufferCreator;

        public BufferResourceHandler(IBufferCreator theBufferCreator)
        {
            _bufferCreator = theBufferCreator;
            Ticket = 0;
            ThreadGroupSize = 128;
            _elementCount = 1000000;
            _resources = new Dictionary<string, BufferResource>();
        }

        public int ThreadGroupSize { get; set; }

        public AbstractResource CreateResource(string theName)
        {
            return new BufferResource(theName, _elementCount, _bufferCreator);
        }

        public void GetThreadGroupInfo(out Int3 threadGroupCount, out Int3 threadGroupSize)
        {
            var _threadGroupCount = (ElementCount + ThreadGroupSize - 1) / ThreadGroupSize;
            _threadGroupCount = Math.Max(1, _threadGroupCount);
            threadGroupCount = new Int3(_threadGroupCount,1,1);
            threadGroupSize = new Int3(ThreadGroupSize,1,1);
        }

        public AbstractResourceBinder CreateBinder(AbstractResource theResource, ShaderNode<Int3> theIndex)
        {
            return new BufferResourceBinder((BufferResource)theResource, new GetMember<Int3,int>(theIndex, "x") );
        }

        public int ElementCount
        {
            get => _elementCount;
            set
            {
                if (value != _elementCount)
                {
                    Ticket = ComputeUtil.GenerateTicket();
                }

                _elementCount = value;
            }
        }

        public int Ticket { get; private set; }
    }
}