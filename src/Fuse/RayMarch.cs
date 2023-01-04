using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{
    public class MarchRay : GpuStruct
    {
        public MarchRay() : base("Ray"){}
    }
    
    public class MakeRay : ShaderNode<MarchRay>
    {
        private ShaderNode<Vector3> _origin;
        private ShaderNode<Vector3> _direction;
        private ShaderNode<Vector3> _surfaceNormal;
        private ShaderNode<Vector3> _surfacePosition;

        public MakeRay(NodeContext nodeContext) : base(nodeContext, "MakeRay")
        {
            
            SetProperty(Structs, @"struct Ray
	{
		float3 origin, direction, surfacePosition, surfaceNormal; 
	};");
        }

        public void SetInputs(ShaderNode<Vector3> theOrigin, ShaderNode<Vector3> theDirection, ShaderNode<Vector3> theSurfacePosition, ShaderNode<Vector3> theSurfaceNormal)
        {
            _origin = theOrigin;
            _direction = theDirection;
            _surfacePosition = theSurfacePosition ?? new ConstantValue<Vector3>(new Vector3());
            _surfaceNormal = theSurfaceNormal ?? new ConstantValue<Vector3>(new Vector3());
            
            SetInputs(new List<AbstractShaderNode>{_origin,_direction,_surfacePosition,_surfaceNormal});
        }
        
        protected override string SourceTemplate()
        {

            return ShaderNodesUtil.Evaluate(@"${resultType} ${resultName};
        ${resultName}.origin = ${origin};
        ${resultName}.direction = ${direction};
        ${resultName}.surfacePosition = ${surfacePosition};
        ${resultName}.surfaceNormal = ${surfaceNormal};
",
                new Dictionary<string, string>
                {
                    {"origin", _origin.ID},
                    {"direction", _direction.ID},
                    {"surfacePosition", _surfacePosition.ID}, 
                    {"surfaceNormal", _surfaceNormal.ID}
                });
        }
    }
}