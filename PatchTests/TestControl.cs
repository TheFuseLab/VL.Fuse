using System;
using System.Collections.Generic;
using Fuse;
using Fuse.compute;
using NUnit.Framework;
using VL.Core;

namespace PatchTests
{
    public static class TestControl
    {
        private static NodeContext _context;
        
        [Test]
        public static void TestBooleanOperator()
        {
            var gpuValue0 = new ValueInput<float>(_context);
            var gpuValue1 = new ValueInput<float>(_context);
            
            var compare = new Operator<float, bool>(_context,new ConstantValue<bool>(_context,true),">");
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(compare.BuildSourceCode());
            
            var compareNull = new Operator<float, bool>(_context, new ConstantValue<bool>(_context,true),">");
            compareNull.SetInput(new List<ShaderNode<float>>{gpuValue0,null});
            Console.WriteLine(compareNull.BuildSourceCode());
        }
        /*
        [Test]
        public static void TestBooleanSwitch()
        {
            var gpuValue0 = new ValueInput<float>();
            var gpuValue1 = new ValueInput<float>();
            
            var compare = new Operator<float, bool>(new ConstantValue<bool>(true),">");
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, null});
            
            var switchVal = new IfNodeDeprecated<float>(new ConstantValue<float>(0));
            switchVal.SetInputs(new List<ShaderNode<float>>{compare, gpuValue0, gpuValue1});
            Console.WriteLine(switchVal.BuildSourceCode());
            
            var switchValNull = new IfNodeDeprecated<float>(compare, gpuValue0, null,new ConstantValue<float>(0));
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(switchValNull.BuildSourceCode());
            
            var switchValNull2 = new IfNodeDeprecated<float>(compare, null, gpuValue1,new ConstantValue<float>(0));
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(switchValNull2.BuildSourceCode());
            
            var switchValNull3 = new IfNodeDeprecated<float>(compare, null, null,new ConstantValue<float>(0));
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(switchValNull3.BuildSourceCode());
        }
        
        [Test]
        public static void TestBooleanSwitchVoid()
        {
            var gpuValue0 = new ValueInput<float>();
            var gpuValue1 = new ValueInput<float>();

            var val0 = new DeclareValue<float>(gpuValue0);
            var val1 = new DeclareValue<float>(gpuValue1);

            var assign0 = new AssignValue<float>(val0, gpuValue0);
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            var assign1 = new AssignValue<float>(val1, gpuValue1);
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            
            var compare = new Operator<float, bool>(gpuValue0, null,new ConstantValue<bool>(true),">");
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            
            var switchVal = new IfNodeDeprecated<GpuVoid>(compare, assign0, assign1);
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(switchVal.BuildSourceCode());
            
            var switchValNull = new IfNodeDeprecated<GpuVoid>(compare, assign0, null);
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(switchValNull.BuildSourceCode());
            
            var switchValNull2 = new IfNodeDeprecated<GpuVoid>(compare, null, assign1);
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(switchValNull2.BuildSourceCode());
            
            var switchValNull3 = new IfNodeDeprecated<float>(compare, null, null);
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(switchValNull3.BuildSourceCode());
        }
        
        [Test]
        public static void TestNumericSwitch()
        {    
            var gpuValueCheck = new ValueInput<int>();
            var gpuValue0 = new ValueInput<float>();
            var gpuValue1 = new ValueInput<float>();
            var gpuValue2 = new ValueInput<float>();
            var gpuValue3 = new ValueInput<float>();
            
            var compare = new Operator<float, bool>(gpuValue0, gpuValue1,new ConstantValue<bool>(false),">");
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(compare.BuildSourceCode());
            
            var switchVal = new SwitchNumeric<float>(gpuValueCheck, new List<ShaderNode<float>>(){gpuValue0, gpuValue1, gpuValue2}, gpuValue3);
            compare.SetInput(new List<ShaderNode<float>>{gpuValue0, gpuValue1});
            Console.WriteLine(switchVal.BuildSourceCode());
        }*/
    }
}