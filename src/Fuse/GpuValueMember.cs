using System.Collections.Generic;

namespace Fuse
{
    public class GpuValueMember<TIn, TOut> : ShaderNode<TOut>
    {

        private readonly GpuValue<TIn> _input;
        private readonly string _member;
        public GpuValueMember(GpuValue<TIn> theInput, string theMember, ConstantValue<TOut> theDefault = null) : base("Member", theDefault)
        {
            _input = theInput;
            _member = theMember;
            Setup(new List<AbstractGpuValue>{theInput});
        }

        protected override string SourceTemplate()
        {
            return ShaderTemplateEvaluator.Evaluate("${resultType} ${resultName} = ${input}.${member};",new Dictionary<string, string> {
                {"input", _input==null? "" : _input.ID},
                {"member", _member}
            });
        }
    }
}