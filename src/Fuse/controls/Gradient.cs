using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Core.Shaders.Ast;

namespace Fuse.controls
{
    public struct GradientPoint : IComparable<GradientPoint>
    {
	    public float Position { get; set; }
	    public Vector4 Color { get; private set; }

        public GradientPoint(float thePosition, Vector4 theColor) {
	        Position = thePosition;
	        Color = theColor;
        }

        public int CompareTo(GradientPoint o) {
            return Position.CompareTo(o.Position);
        }
	
        public override string ToString() {
            return Position + " : " + Color;
        }
    }
    
    public class Gradient : List<GradientPoint>
    {
        public Gradient(){
		
		}
	
	public void Add(float thePosition, Vector4 theColor){
		Add(new GradientPoint(thePosition, theColor.DeepClone()));
	}

	public Gradient Clone(){
		var myResult = new Gradient();
		ForEach(point => myResult.Add(point));
		return myResult;
	}
	
	public void Set(Gradient theGradient){
		if(theGradient == this)return;
		Clear();
		theGradient.ForEach(point => Add(point));
	}
	
	
	public new void Add(GradientPoint e) {
		base.Add(e);
		Sort();
	}
	
	/*
	 * Interpolate a color on the gradient
	 * @param theBlend a value from 0 to 1 that represent the position between the first control point and the last one
	 * @return the position
	 */
	/*
	public Vector4 Interpolate (double thePosition){
		
		if(Count == 0)return new Vector4();
		if(Count == 1){
			return this[0].Color;
		}
		
		var myIndex = -1;
		
		for(var i = 0; i < Count;i++)
		{
			if (!(this[i].Position > thePosition)) continue;
			myIndex = i;
			break;
		}
		
		if(myIndex == -1)return this[Count - 1].Color;
		if(myIndex == 0)return this[0].Color;
		
		double myPos0 = this[myIndex - 1].Position;
		double myPos1 = this[myIndex].Position;
		double myBlend = CCMath.smoothStep(myPos0, myPos1, thePosition);
		
		return Vector4.blend(this[myIndex - 1].Color, this[myIndex].Color, myBlend);
	}

	public Gradient Blend(Gradient theB, double theScalar) {
		if(theScalar <= 0)return  Clone();
		if(theScalar >= 1)return theB.Clone();
		
		var myResult = new Gradient();
		
		this.ForEach(myPoint => myResult.Add(new GradientPoint(myPoint.Position, Interpolate(myPoint.Position).blend(theB.Interpolate(myPoint.Position), theScalar))));
		theB.ForEach(myPoint => myResult.Add(new GradientPoint(myPoint.Position, Interpolate(myPoint.Position).blend(theB.Interpolate(myPoint.Position), theScalar))));

		return myResult;
	}
	*/
	
	/*
	@Override
	public Map<String, Object> data() {
		List<Map<String, Object>> myPoints = new ArrayList<>();
		for(GradientPoint myPoint:this) {
			Map<String, Object>myPointData = myPoint.color().data();
			myPointData.put("position", myPoint.position());
			myPoints.add(myPointData);
		}
		
		Map<String, Object> myResult = new HashMap<>();
		myResult.put(CCBlendable.BLENDABLE_TYPE_ATTRIBUTE, getClass().getName());
		myResult.put("points", myPoints);
		return myResult;
	}
	
	@SuppressWarnings("unchecked")
	@Override
	public void data(Map<String, Object> theData) {
		List<Map<String, Object>> myPoints = (List<Map<String, Object>>)theData.get("points");

		for(Map<String, Object> myPointData:myPoints) {
			double myPosition = (Double)myPointData.get("position");
			Vector4 myColor = new Vector4();
			myColor.data(myPointData);
			add(new GradientPoint(myPosition, myColor));
		}
	}*/
    }
}