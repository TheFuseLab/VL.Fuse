using System;
using System.Collections.Generic;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

namespace Fuse.Tests
{
    public class TestDelegate
    {
        public static void TestDelegateFunction()
        {
            var delegate0 = new FunctionParameter<float>(new ConstantValue<float>(0),0);
            var delegate1 = new FunctionParameter<float>(new ConstantValue<float>(0),1);
            
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();
            
            var sin2 = new IntrinsicFunctionNode<float>(
                new List< AbstractGpuValue> {delegate0.Output},
                "sin", new ConstantValue<float>(0));
            var operatorNode = new OperatorNode<float, float>(new List<GpuValue<float>> {sin2.Output, delegate1.Output},new ConstantValue<float>(0),"+");
            
            var delegateNode = new FunctionInvokeNode<float>(operatorNode.Output, new List< AbstractGpuValue> {gpuValue0.Output,gpuValue1.Output}, "delegate");
            
            Console.WriteLine(operatorNode.BuildSourceCode());
            
            delegateNode.FunctionMap().ForEach(kv => Console.Write(kv.Value));
            
            Console.WriteLine(delegateNode.BuildSourceCode());
        }

        public static void TestTemplateDelegateFunction()
        {
            var offset = new FunctionParameter<Vector2>(ConstantHelper.FromFloat<Vector2>(0));

            const string cellDistanceCode = @"float get(float2 offset)
 {
    return  sqrt(dot( offset, offset ));
}";
            
            var cellDistance = new CustomFunctionNode<float>(
                new List<AbstractGpuValue> {offset.Output}, 
                "cellDistance",
                cellDistanceCode, 
                new ConstantValue<float>(0)
            );
            
            
            var f1f2 = new FunctionParameter<Vector2>(ConstantHelper.FromFloat<Vector2>(0));
            const string cellFunctionCode = @"float get(float2 offset)
{
    return  sqrt(dot( offset, offset ));
}";
            
            var cellFunction = new CustomFunctionNode<float>(
                new List<AbstractGpuValue> {f1f2.Output}, 
                "cellFunction",
                cellFunctionCode, 
                new ConstantValue<float>(0)
            );
            
            var x = new GpuInput<Vector2>();
            var inputs = new List<AbstractGpuValue>
            {
                x.Output
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
                new List<AbstractGpuValue> {x.Output}, 
                "worley",
                worleyCode, 
                new ConstantValue<float>(0),  
                new List<IFunctionInvokeNode>
                {
                    new FunctionInvokeNode<float>(cellDistance.Output, new List<AbstractGpuValue> {x.Output},"cellDistance"),
                    new FunctionInvokeNode<float>(cellFunction.Output, new List<AbstractGpuValue> {f1f2.Output},"cellFunction")
                }
            );
            Console.WriteLine(customFunction.FunctionMap());
            Console.WriteLine(customFunction.BuildSourceCode());
        }

        public static void TestFBM()
        {
            var noiseDelegateParameter = new FunctionParameter<Vector2>(ConstantHelper.FromFloat<Vector2>(0));
            var noiseFunction = new MixinFunctionNode<float>(
                new List<AbstractGpuValue>(){noiseDelegateParameter.Output}, 
                "gradientNoise12",
                new ConstantValue<float>(0),
                "GradientNoise"
                );
            var fbmTypeCode =@"${resultType} ${signature}(${argumentType} p){
    return ${noise}(p);
}";
            var fbmTypeDelegateParameter = new FunctionParameter<float>(new ConstantValue<float>(0));
            var fbmTypeFunction = new CustomFunctionNode<float>(
                new List<AbstractGpuValue>(){fbmTypeDelegateParameter.Output}, 
                "fbmType",
                fbmTypeCode,
                new ConstantValue<float>(0),
                new List<IFunctionInvokeNode>
                {
                    new FunctionInvokeNode<float>(noiseFunction.Output, new List<AbstractGpuValue> {fbmTypeDelegateParameter.Output},"noise")
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
                new List<AbstractGpuValue>(){gpuValue0.Output}, 
                "fbm",
                fbmCode,
                new ConstantValue<float>(0),
                new List<IFunctionInvokeNode>
                {
                    new FunctionInvokeNode<float>(fbmTypeFunction.Output, new List<AbstractGpuValue> {gpuValue0.Output},"fbmType")
                }
            );
            
            Console.WriteLine(fbmFunction.FunctionMap());
            Console.WriteLine(fbmFunction.BuildSourceCode());
        }
    }
}