using System.Collections.Generic;

namespace Fuse
{
    public class BooleanSwitchNodeNew<T> : ShaderNode<T>
    {
        private readonly GpuValue<bool> _inCheck;
        private readonly GpuValue<T> _inFalse;
        private readonly GpuValue<T> _inTrue;
        
        public BooleanSwitchNodeNew(GpuValue<bool> inCheck, GpuValue<T> inFalse, GpuValue<T> inTrue, GpuValue<T> theDefault = null) : base( "expression", theDefault)
        {

            var ins = new List<AbstractGpuValue>(){inCheck};
            _inCheck = inCheck;
            _inFalse = inFalse;
            _inTrue = inTrue;
            
            if(_inFalse != null)ins.Add(_inFalse);
            if(_inTrue != null)ins.Add(_inTrue);
            if(_inFalse == null && _inTrue == null)ins.Add(null);
            
            Setup(ins);
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${check} ? ${inTrue} : ${inFalse};", 
                new Dictionary<string, string>
                {
                    {"check", _inCheck.ID},
                    {"inFalse", _inFalse.ID},
                    {"inTrue", _inTrue.ID}
                });
        }
    }
}