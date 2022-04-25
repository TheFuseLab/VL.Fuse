using System;
using System.Collections.Generic;
using Fuse;
using NUnit.Framework;
using Stride.Core.Mathematics;

namespace PatchTests
{
    public static class TestDelegate
    {
        [Test]
        public static void TestDelegateFunction()
        {
            var delegate0 = new FunctionParameter<float>(new ConstantValue<float>(0),0);
            var delegate1 = new FunctionParameter<float>(new ConstantValue<float>(0),1);
            
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();
            
            var sin2 = new IntrinsicFunctionNode<float>(
                new List< AbstractShaderNode> {delegate0},
                "sin", new ConstantValue<float>(0));
            var operatorNode = new OperatorNode<float, float>(new List<ShaderNode<float>> {sin2, delegate1},new ConstantValue<float>(0),"+");
            
            var delegateNode = new FunctionInvokeNode<float>(operatorNode, new List< AbstractShaderNode> {gpuValue0,gpuValue1}, "delegate");
            
            Console.WriteLine(operatorNode.BuildSourceCode());
            
            delegateNode.FunctionMap().ForEach(kv => Console.Write(kv.Value));
            
            Console.WriteLine(delegateNode.BuildSourceCode());
        }
        [Test]
        public static void TestTemplateDelegateFunction()
        {
            var offset = new FunctionParameter<Vector2>(ConstantHelper.FromFloat<Vector2>(0));

            const string cellDistanceCode = @"float get(float2 offset)
 {
    return  sqrt(dot( offset, offset ));
}";
            
            var cellDistance = new CustomFunctionNode<float>(
                new List<AbstractShaderNode> {offset}, 
                "cellDistance",
                cellDistanceCode, 
                new ConstantValue<float>(0)
            );
            
            
            var f1F2 = new FunctionParameter<Vector2>(ConstantHelper.FromFloat<Vector2>(0));
            const string cellFunctionCode = @"float get(float2 offset)
{
    return  sqrt(dot( offset, offset ));
}";
            
            var cellFunction = new CustomFunctionNode<float>(
                new List<AbstractShaderNode> {f1F2}, 
                "cellFunction",
                cellFunctionCode, 
                new ConstantValue<float>(0)
            );
            
            var x = new GpuInput<Vector2>();
            var inputs = new List<AbstractShaderNode>
            {
                x
            };
            var worleyCode = @"
float ${signature}(float2 p)
{
    float2 n = floor( p );
    float2 f = frac( p );

    float f1 = 8.0;
    float f2 = 8.0;
    
    
    for( int j=-1; j<=1; j++ )
    for( int i=-1; i<=1; i++ )
    {
        float2 g = float2(i,j);
        float2 o = hash22( n + g );
        float2 r = g - f + o;

        float d = ${cellDistance}(r);

        if( d<f1 ) 
        { 
            f2 = f1; 
            f1 = d; 
        }
        else if( d<f2 ) 
        {
            f2 = d;
        }
    }
    
    return ${cellFunction}(float2(f1, f2));
}";
            var customFunction = new CustomFunctionNode<float>(
                new List<AbstractShaderNode> {x}, 
                "worley",
                worleyCode, 
                new ConstantValue<float>(0),  
                new List<IFunctionInvokeNode>
                {
                    new FunctionInvokeNode<float>(cellDistance, new List<AbstractShaderNode> {x},"cellDistance"),
                    new FunctionInvokeNode<float>(cellFunction, new List<AbstractShaderNode> {f1F2},"cellFunction")
                }
            );
            Console.WriteLine(customFunction.FunctionMap());
            Console.WriteLine(customFunction.BuildSourceCode());
        }
        [Test]
        public static void TestFBM()
        {
            var noiseDelegateParameter = new FunctionParameter<Vector2>(ConstantHelper.FromFloat<Vector2>(0));
            var noiseFunction = new MixinFunctionNode<float>(
                new List<AbstractShaderNode>(){noiseDelegateParameter}, 
                "gradientNoise12",
                new ConstantValue<float>(0),
                "GradientNoise"
                );
            var fbmTypeCode =@"${resultType} ${signature}(${argumentType} p){
    return ${noise}(p);
}";
            var fbmTypeDelegateParameter = new FunctionParameter<float>(new ConstantValue<float>(0));
            var fbmTypeFunction = new CustomFunctionNode<float>(
                new List<AbstractShaderNode>(){fbmTypeDelegateParameter}, 
                "fbmType",
                fbmTypeCode,
                new ConstantValue<float>(0),
                new List<IFunctionInvokeNode>
                {
                    new FunctionInvokeNode<float>(noiseFunction, new List<AbstractShaderNode> {fbmTypeDelegateParameter},"noise")
                }
            );

            var fbmCode = @"${resultType} ${signature}(${argumentType} p,float gain, float octaves, float lacunarity)
{

    float myScale = 1;
    float myFallOff = gain;

    int iOctaves = int(floor(octaves)); 
    ${resultType} myResult = 0.;  
    float myAmp = 0.;

    for(int i = 0; i < iOctaves;i++){
        myResult += ${fbmType}(p * myScale) * myFallOff;
        myAmp += myFallOff;
        myFallOff *= gain;
        myScale *= lacunarity;
    }

    float myBlend = octaves - float(iOctaves);
    myResult += ${fbmType}(p * myScale) * myFallOff * myBlend;    
    myAmp += myFallOff * myBlend;
    
    if(myAmp > 0.0){
        myResult /= myAmp;
    }
 
    return myResult;
}";
            var gpuValue0 = new GpuInput<Vector2>();
            var fbmFunction = new CustomFunctionNode<float>(
                new List<AbstractShaderNode>(){gpuValue0}, 
                "fbm",
                fbmCode,
                new ConstantValue<float>(0),
                new List<IFunctionInvokeNode>
                {
                    new FunctionInvokeNode<float>(fbmTypeFunction, new List<AbstractShaderNode> {gpuValue0},"fbmType")
                }
            );
            
            Console.WriteLine(fbmFunction.FunctionMap());
            Console.WriteLine(fbmFunction.BuildSourceCode());
        }
    }
}