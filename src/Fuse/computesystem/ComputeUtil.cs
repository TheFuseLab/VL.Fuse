using System;
using Stride.Graphics;
using VL.Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.ComputeSystem
{
    public static class ComputeUtil
    {
        public static PixelFormat GetFormatFromStride(int theStride)
        {
            return theStride switch
            {
                4 => PixelFormat.R32_Float,
                8 => PixelFormat.R32G32_Float,
                12 => PixelFormat.R32G32B32_Float,
                16 => PixelFormat.R32G32B32A32_Float,
                _ => PixelFormat.None
            };
        }

        private static int ReplaceValue(int theInput, int theReplaceValue)
        {
            return theReplaceValue > 0 ? theReplaceValue : theInput;
        }
        
        

        public static void GetCountAndStrideFormat(
            IGraphicsDataProvider theProvider, 
            int theElementCount, 
            int theElementSizeInBytes, 
            bool theIsStructuredBuffer, 
            bool theAllowRawViews, 
            out int theSize, 
            out int theStructureStride,
            out PixelFormat theFormat)
        {
            var sizeInBytes = 0;
            var elementSizeInBytes = 4;
            if (theProvider != null)
            {
                sizeInBytes = theProvider.SizeInBytes;
                elementSizeInBytes = theProvider.ElementSizeInBytes;
            }

            var isStructuredBuffer = theIsStructuredBuffer && !theAllowRawViews;

            theFormat = isStructuredBuffer ? PixelFormat.None : GetFormatFromStride(elementSizeInBytes);

            elementSizeInBytes = ReplaceValue(elementSizeInBytes, theElementSizeInBytes);
            theStructureStride = isStructuredBuffer ? elementSizeInBytes : 0;
            
            sizeInBytes = ReplaceValue(sizeInBytes, theElementCount * elementSizeInBytes);
            theSize = Math.Max(sizeInBytes, 4);
        }
        
        public static Buffer ComputeBuffer(
            GraphicsDevice theGraphicsDevice,
            int theElementCount, 
            int theElementSizeInBytes,
            IGraphicsDataProvider theInitialData,
            bool theIsUnorderedAccess,
            bool theIsStructuredBuffer,
            bool theIsStreamOutput,
            bool theAllowRawVies,
            bool theDoRecreate)
        {
            GetCountAndStrideFormat(
                theInitialData, 
                theElementCount, 
                theElementSizeInBytes, 
                theIsStructuredBuffer, 
                theAllowRawVies, 
                out var sizeInBytes, 
                out var structureStride, 
                out var format);

            var graphicsResourceUsage = GraphicsResourceUsage.Default;

            var bufferFlags = new BufferFlags();
            
            var bufferDescription = new BufferDescription
            {
                SizeInBytes = theElementCount * theElementSizeInBytes
            };
            var bufferViewDescription = new BufferViewDescription();
            
            
           // var buffer = Buffer.New(bufferDescription,bufferViewDescription,null,true);
            
            
            
            return null;
        }
    }
}