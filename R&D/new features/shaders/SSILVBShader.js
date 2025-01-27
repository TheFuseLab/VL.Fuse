import {
	DataTexture,
	Matrix4,
	RepeatWrapping,
	Vector2,
	Vector3,
	GLSL3
} from 'three';

/**
 * References:
 * - implemented algorithm - SSILVB
 *   - https://cybereality.com/screen-space-indirect-lighting-with-visibility-bitmask-improvement-to-gtao-ssao-real-time-ambient-occlusion-algorithm-glsl-shader-implementation/
 *   - https://cdrinmatane.github.io/posts/ssaovb-code/
 */

const SSILVBShader = {

	name: 'SSILVBShader',

	defines: {
		PERSPECTIVE_CAMERA: 1,
		SAMPLES: 16,
		NORMAL_VECTOR_TYPE: 1,
		DEPTH_SWIZZLING: 'x',
		SCREEN_SPACE_RADIUS: 0.0,
		SCREEN_SPACE_RADIUS_SCALE: 100.0,
		SCENE_CLIP_BOX: 0.0,
		SLICES: 4,
	},

	uniforms: {
		tNormal: { value: null },
		tDepth: { value: null },
		tColor: { value: null },
		tNoise: { value: null },
		resolution: { value: new Vector2() },
		cameraNear: { value: null },
		cameraFar: { value: null },
		cameraProjectionMatrix: { value: new Matrix4() },
		cameraProjectionMatrixInverse: { value: new Matrix4() },
		cameraWorldMatrix: { value: new Matrix4() },
		radius: { value: 12.0 },
		distanceExponent: { value: 1.7 },
		thickness: { value: 1. },
		scale: { value: 1. },
		sceneBoxMin: { value: new Vector3( - 1, - 1, - 1 ) },
		sceneBoxMax: { value: new Vector3( 1, 1, 1 ) },
	},

	glslVersion: GLSL3,

	vertexShader: /* glsl */`

		varying vec2 vUv;

		void main() {
			vUv = uv;
			gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );
		}`,

	fragmentShader: /* glsl */`
		// Adapted from "Screen Space Indirect Lighting with Visibility Bitmask" by Olivier Therrien, et al.
		// https://cdrinmatane.github.io/posts/cgspotlight-slides/

		#define MAX_RAY 32u

		precision highp float;
		precision highp sampler2D;

		varying vec2 vUv;
		uniform sampler2D tNormal;
		uniform sampler2D tDepth;
		uniform sampler2D tColor;
		uniform sampler2D tNoise;

		uniform vec2 resolution;
		uniform float cameraNear;
		uniform float cameraFar;
		uniform mat4 cameraProjectionMatrix;
		uniform mat4 cameraProjectionMatrixInverse;		
		uniform mat4 cameraWorldMatrix;
		uniform float radius;
		uniform float distanceExponent;
		uniform float thickness;
		uniform float scale;
		uniform bool useCorrectNormals;
		uniform vec2 _ScreenParams;
		#if SCENE_CLIP_BOX == 1
			uniform vec3 sceneBoxMin;
			uniform vec3 sceneBoxMax;
		#endif

		const float pi = 3.14159265359;
		const float twoPi = 2.0 * pi;
		const float halfPi = 0.5 * pi;
		const float sliceCount = 4.0;
		float sampleCount = float(SAMPLES);

		#include <common>
		#include <packing>

		vec3 getViewPosition(const in vec2 screenPosition, const in float depth) {
			vec4 clipSpacePosition = vec4(vec3(screenPosition, depth) * 2.0 - 1.0, 1.0);
			vec4 viewSpacePosition = cameraProjectionMatrixInverse * clipSpacePosition;
			return viewSpacePosition.xyz / viewSpacePosition.w;
		}

		vec3 getWorldPosition(const in vec2 screenPosition, const in float depth) {
			vec3 viewSpacePosition = getViewPosition(screenPosition, depth);
			return (cameraWorldMatrix * vec4(viewSpacePosition, 1.0)).xyz;
		}

		float getDepth(const vec2 uv) {  
			return textureLod(tDepth, uv.xy, 0.0).DEPTH_SWIZZLING;
		}

		float fetchDepth(const ivec2 uv) {   
			return texelFetch(tDepth, uv.xy, 0).DEPTH_SWIZZLING;
		}

		float getViewZ(const in float depth) {
			#if PERSPECTIVE_CAMERA == 1
				return perspectiveDepthToViewZ(depth, cameraNear, cameraFar);
			#else
				return orthographicDepthToViewZ(depth, cameraNear, cameraFar);
			#endif
		}

		vec3 computeNormalFromDepth(const vec2 uv) {
			vec2 size = vec2(textureSize(tDepth, 0));
			ivec2 p = ivec2(uv * size);
			float c0 = fetchDepth(p);
			float l2 = fetchDepth(p - ivec2(2, 0));
			float l1 = fetchDepth(p - ivec2(1, 0));
			float r1 = fetchDepth(p + ivec2(1, 0));
			float r2 = fetchDepth(p + ivec2(2, 0));
			float b2 = fetchDepth(p - ivec2(0, 2));
			float b1 = fetchDepth(p - ivec2(0, 1));
			float t1 = fetchDepth(p + ivec2(0, 1));
			float t2 = fetchDepth(p + ivec2(0, 2));
			float dl = abs((2.0 * l1 - l2) - c0);
			float dr = abs((2.0 * r1 - r2) - c0);
			float db = abs((2.0 * b1 - b2) - c0);
			float dt = abs((2.0 * t1 - t2) - c0);
			vec3 ce = getViewPosition(uv, c0).xyz;
			vec3 dpdx = (dl < dr) ? ce - getViewPosition((uv - vec2(1.0 / size.x, 0.0)), l1).xyz : -ce + getViewPosition((uv + vec2(1.0 / size.x, 0.0)), r1).xyz;
			vec3 dpdy = (db < dt) ? ce - getViewPosition((uv - vec2(0.0, 1.0 / size.y)), b1).xyz : -ce + getViewPosition((uv + vec2(0.0, 1.0 / size.y)), t1).xyz;
			return normalize(cross(dpdx, dpdy));
		}

		vec3 getViewNormal(const vec2 uv) {
			#if NORMAL_VECTOR_TYPE == 2
				return normalize(textureLod(tNormal, uv, 0.).rgb);
			#elif NORMAL_VECTOR_TYPE == 1
				return unpackRGBToNormal(textureLod(tNormal, uv, 0.).rgb);
			#else
				return computeNormalFromDepth(uv);
			#endif
		}

		vec3 getWorldNormal(const vec2 uv) {
			return normalize((cameraWorldMatrix * vec4(getViewNormal(uv), 0.0)).xyz);
		}

		vec3 getSceneUvAndDepth(vec3 sampleViewPos) {
			vec4 sampleClipPos = cameraProjectionMatrix * vec4(sampleViewPos, 1.);
			vec2 sampleUv = sampleClipPos.xy / sampleClipPos.w * 0.5 + 0.5;
			float sampleSceneDepth = getDepth(sampleUv);
			return vec3(sampleUv, sampleSceneDepth);
		}

		// https://blog.demofox.org/2022/01/01/interleaved-gradient-noise-a-different-kind-of-low-discrepancy-sequence/
		float randf(int x, int y) {
			return mod(52.9829189 * mod(0.06711056 * float(x) + 0.00583715 * float(y), 1.0), 1.0);
		}

		// From http://byteblacksmith.com/improvements-to-the-canonical-one-liner-glsl-rand-for-opengl-es-2-0/
		float rand2(vec2 co) {
			float a = 12.9898;
			float b = 78.233;
			float c = 43758.5453;
			float dt = dot(co.xy, vec2(a, b));
			float sn = mod(dt, 3.14);
			return fract(sin(sn) * c);
		}

		vec2 GTAOFastAcos(vec2 x) {
			vec2 outVal = -0.156583 * abs(x) + halfPi;
			outVal *= sqrt(1.0 - abs(x));
			//return x >= 0.0 ? outVal : pi - outVal; // uhhh does this really work in HLSL?
			return vec2(x.x >= 0.0 ? outVal.x : pi - outVal.x, 
						x.y >= 0.0 ? outVal.y : pi - outVal.y);
		}

		float GTAOFastAcos(float x) {
			float outVal = -0.156583 * abs(x) + halfPi;
			outVal *= sqrt(1.0 - abs(x));
			return x >= 0.0 ? outVal : pi - outVal;
		}

		// https://graphics.stanford.edu/%7Eseander/bithacks.html
		uint bitCount(uint value) {
			value = value - ((value >> 1u) & 0x55555555u);
			value = (value & 0x33333333u) + ((value >> 2u) & 0x33333333u);
			return ((value + (value >> 4u) & 0xF0F0F0Fu) * 0x1010101u) >> 24u;
		}

		//// https://graphics.stanford.edu/%7Eseander/bithacks.html
		//uint bitCount(uint v) {
		//	v = v - ((v >> 1u) & 0x55555555u);                    // reuse input as temporary
		//	v = (v & 0x33333333u) + ((v >> 2u) & 0x33333333u);     // temp
		//	return ((v + (v >> 4u) & 0xF0F0F0Fu) * 0x1010101u) >> 24u; // count
		//}

		//uint bitCount(uint value) {
		//	uint count = 0u;
		//	while (value != 0u) {
		//		count++;
		//		value &= value - 1u;
		//	}
		//	return count;
		//}

		//uint bitCount(uint x) {
		//	uint i;
		//	uint res = 0u;
		//	for(uint i = 0u; i < 32u; i++) {
		//		uint mask = 1u << i;
		//		if ((x & mask) == 1u) { res ++; }
		//	}
		//	return res;
		//}

		const uint sectorCount = 32u;
		uint ComputeOccludedBitfield(float minHorizon, float maxHorizon, inout uint globalOccludedBitfield, out uint numOccludedZones) {
			uint startHorizonInt = uint(minHorizon * float(MAX_RAY));
			uint angleHorizonInt = uint(ceil((maxHorizon - minHorizon) * float(MAX_RAY)));
			uint angleHorizonBitfield = angleHorizonInt > 0u ? uint(0xFFFFFFFFu >> (MAX_RAY - angleHorizonInt)) : 0u;
			uint currentOccludedBitfield = angleHorizonBitfield << startHorizonInt;
			currentOccludedBitfield = currentOccludedBitfield & (~globalOccludedBitfield);
			globalOccludedBitfield = globalOccludedBitfield | currentOccludedBitfield;
			numOccludedZones = bitCount(currentOccludedBitfield);
			return currentOccludedBitfield;
		}

		// From http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
		// All components are in the range [0…1], including hue.
		vec3 RgbToHsv(vec3 c) {
			vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
			vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
			vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));

			float d = q.x - min(q.w, q.y);
			float e = 1.0e-10;
			return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
		}
		vec3 HsvToRgb(vec3 c) {
			vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
			vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
			return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
		}

		// Approximates luminance from an RGB value
		// https://github.com/mrdooz/kumi/blob/master/effects/luminance.hlsl
		float Luminance(vec3 color) {
			return dot(color, vec3(0.299f, 0.587f, 0.114f));
		}

		vec3 HorizonSampling(bool directionIsRight, float radius, vec3 posVS, vec2 slideDir_TexelSize, float initialRayStep, 
			vec2 uv, vec3 viewDir, vec3 normalVS, float n, inout uint globalOccludedBitfield, vec3 planeNormal, inout vec3 debug) {

			int _StepCount = int(sampleCount);
			//        var renderResolution = new Vector2(cam.pixelWidth, cam.pixelHeight);
        	// halfprojScale = (float)renderResolution.y / (Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * 2) * 0.5f;
			//_HalfProjScale

			float stepRadius = (radius * (resolution.x * 0.5)) / float(_StepCount);
			stepRadius /= (float(_StepCount + 1));
			float radiusVS = max(1.0, float(_StepCount-1)) * stepRadius;
			float samplingDirection = directionIsRight ? 1.0 : -1.0;
			vec3 col = vec3(0.0);
			vec3 lastSamplePosVS = posVS;
				
			for (uint j = 0u; j < uint(_StepCount); j++) {

				float offset = pow(abs((stepRadius * (float(j) + initialRayStep)) / radiusVS), distanceExponent) * radiusVS;

				vec2 uvOffset = slideDir_TexelSize * max(offset, 1.0 + float(j));
				vec2 sampleUV = uv + uvOffset * samplingDirection;


				if(sampleUV.x <= 0.0 || sampleUV.y <= 0.0 || sampleUV.x >= 1.0 || sampleUV.y >= 1.0)
					break;
				
				bool _MipOptimization = false;
				int mipLevelOffset = 0;//_MipOptimization ? min((int(j) + 1) / 2, 4) : 0;
				vec3 samplePosVS = getViewPosition(sampleUV, getDepth(sampleUV));
				samplePosVS.z = -samplePosVS.z;
				vec3 pixelToSample = normalize(samplePosVS - posVS);
				bool _LinearThickness = false;
				//float _Thickness = 1.0;
				float linearThicknessMultiplier = 1.0; //_LinearThickness ? saturate(samplePosVS.z / _ProjectionParams.z) * 100 : 
				vec3 pixelToSampleBackface = normalize((samplePosVS - (linearThicknessMultiplier * viewDir * thickness )) - posVS);

				vec2 frontBackHorizon = vec2(dot(pixelToSample, viewDir), dot(pixelToSampleBackface, viewDir));
				frontBackHorizon = directionIsRight ? frontBackHorizon.yx : frontBackHorizon.xy; // Front/Back get inverted depending on angle
				frontBackHorizon = GTAOFastAcos(clamp(frontBackHorizon, -1.0, 1.0));
				frontBackHorizon = saturate(((samplingDirection * -frontBackHorizon) - n + halfPi) / pi);
				//frontBackHorizon = saturate(frontBackHorizon*(1.0+1.5*pi/float(MAX_RAY))-1.5*halfPi/float(MAX_RAY)); // Remamp bitfield on one sector narrower hemisphere
				frontBackHorizon = directionIsRight ? frontBackHorizon.yx : frontBackHorizon.xy; // Front/Back get inverted depending on angle
				frontBackHorizon = directionIsRight ? 1.0 - frontBackHorizon : frontBackHorizon;

				uint numOccludedZones;
				ComputeOccludedBitfield(frontBackHorizon.x, frontBackHorizon.y, globalOccludedBitfield, numOccludedZones);

				vec3 lightNormalVS = vec3(0.0);
				if(numOccludedZones > 0u) // If a ray hit the sample, that sample is visible from shading point
				{
					//vec3 lightColor = SAMPLE_TEXTURE2D_X_LOD(_LightPyramidTexture, s_point_clamp_sampler, sampleUV, mipLevelOffset).rgb;
					vec3 lightColor = texture(tColor, sampleUV).rgb; // Consider mipLevelOffset
					if(Luminance(lightColor) > 0.001) // Continue if there is light at that location (intensity > 0)
					{
						vec3 lightDirectionVS = normalize(pixelToSample);
						float normalDotLightDirection = saturate(dot(normalVS, lightDirectionVS));

						if (normalDotLightDirection > 0.001) // Continue if light is facing surface normal
						{
							//#ifdef NORMAL_APPROXIMATION
							//lightNormalVS = -samplingDirection * cross(normalize(samplePosVS-lastSamplePosVS), planeNormal);
							//#else
							//lightNormalVS = GetNormalPyramidVS(sampleUV, mipLevelOffset); //GetNormalVS(sampleUV * resolution.xy);
							//#endif
							lightNormalVS = getViewNormal(sampleUV);
							
							float _BackfaceLighting = 0.0;

							// Intensity of outgoing light in the direction of the shading point
							float lightNormalDotLightDirection = dot(lightNormalVS, -lightDirectionVS);
							lightNormalDotLightDirection = _BackfaceLighting > 0.0 && dot(lightNormalVS, viewDir) > 0.0 ? 
								(sign(lightNormalDotLightDirection) < 0.0 ? abs(lightNormalDotLightDirection) * _BackfaceLighting : abs(lightNormalDotLightDirection)) : 
								saturate(lightNormalDotLightDirection);
							
							col.xyz += (float(numOccludedZones)/float(MAX_RAY)) * lightColor * normalDotLightDirection * lightNormalDotLightDirection;
						}
					}
				}
				lastSamplePosVS = samplePosVS;
			}
			
			return col;
		}


		// get indirect lighting and ambient occlusion
		void main() {

			vec2 aspect = vec2(cameraProjectionMatrix[0][0] / cameraProjectionMatrix[1][1], 1.0);
			float depth = getDepth(vUv.xy);
			if (depth >= 1.0) { discard; return; }

			vec3 posVS = getViewPosition(vUv, depth);
			posVS.z = -posVS.z;
			vec3 normalVS = normalize(getViewNormal(vUv.xy));
			normalVS = vec3(normalVS.xy, -normalVS.z);
			vec3 viewDir = normalize(-posVS);

			float random = randf(int(gl_FragCoord.x), int(gl_FragCoord.y));
			float _TemporalDirections = random; // TODO: TemporalDirections
			float noiseOffset         = random;//SpatialOffsets(gl_FragCoord.xy);   // TODO: SpatialOffsets
			float noiseDirection      = random;//GradientNoise(gl_FragCoord.xy); // TODO: GradientNoise
			//float initialRayStep = fract(noiseOffset + _TemporalOffsets) + (randf(int(gl_FragCoord.x), int(gl_FragCoord.y)) * 2.0 - 1.0) * 1.0 * float(1); // 1 or 0
			float initialRayStep = (random * 18.0 - 8.0);

			int _RotationCount = SLICES;

    		float ao = 0.0;
    		vec3 col = vec3(0.0);

			//float radius = 12.0;
			vec3 debug = vec3(0.0);

			//int i = 0;
			for (int i = 0; i < _RotationCount; i++) {
				float rotationAngle = (float(i) + noiseDirection + _TemporalDirections) * (pi / float(_RotationCount));
				vec3 sliceDir = vec3(vec2(cos(rotationAngle), sin(rotationAngle)), 0.0);
				vec2 slideDir_TexelSize = sliceDir.xy * (1.0 / resolution.xy);
				uint globalOccludedBitfield = 0u;
				
				vec3 planeNormal = normalize(cross(sliceDir, viewDir));
				vec3 tangent = cross(viewDir, planeNormal);
				vec3 projectedNormal = normalVS - planeNormal * dot(normalVS, planeNormal);
				vec3 projectedNormalNormalized = normalize(projectedNormal);
				vec3 realTangent = cross(projectedNormalNormalized, planeNormal);
				
				float cos_n = clamp(dot(projectedNormalNormalized, viewDir), -1.0, 1.0);
				float n = -sign(dot(projectedNormal, tangent)) * acos(cos_n);
				
				col += HorizonSampling( true, radius, posVS, slideDir_TexelSize, initialRayStep, vUv, viewDir, normalVS, n, globalOccludedBitfield, planeNormal, debug);
				col += HorizonSampling(false, radius, posVS, slideDir_TexelSize, initialRayStep, vUv, viewDir, normalVS, n, globalOccludedBitfield, planeNormal, debug);
				
				ao += float(bitCount(globalOccludedBitfield)) / float(MAX_RAY);
			}

			float _AOIntensity = scale;
			ao /= float(_RotationCount);
			ao = saturate(pow(1.0-saturate(ao), _AOIntensity));

			col /= float(_RotationCount);

			float _GIIntensity = 10.0;
			col = col * _GIIntensity;

			// Clamp the final saturation
			//col = RgbToHsv(col);
			//col.z = clamp(col.z, 0.0, 7); 
			//// Convert back to HSV space
			//col = HsvToRgb(col);
			col = RgbToHsv(col);
			col.z = clamp(col.z, 0.0, 0.7); 
			// Convert back to HSV space
			col = HsvToRgb(col);

			gl_FragColor = vec4(ao, ao, ao, 1.0); // col, ao); //vec4(viewDir.z, viewDir.z, viewDir.z, 1.0); //
		}`

};

const SSILVBDepthShader = {

	name: 'SSILVBDepthShader',

	defines: {
		PERSPECTIVE_CAMERA: 1
	},

	uniforms: {
		tDepth: { value: null },
		cameraNear: { value: null },
		cameraFar: { value: null },
	},

	vertexShader: /* glsl */`
		varying vec2 vUv;

		void main() {
			vUv = uv;
			gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );
		}`,

	fragmentShader: /* glsl */`
		uniform sampler2D tDepth;
		uniform float cameraNear;
		uniform float cameraFar;
		varying vec2 vUv;

		#include <packing>

		float getLinearDepth( const in vec2 screenPosition ) {
			#if PERSPECTIVE_CAMERA == 1
				float fragCoordZ = texture2D( tDepth, screenPosition ).x;
				float viewZ = perspectiveDepthToViewZ( fragCoordZ, cameraNear, cameraFar );
				return viewZToOrthographicDepth( viewZ, cameraNear, cameraFar );
			#else
				return texture2D( tDepth, screenPosition ).x;
			#endif
		}

		void main() {
			float depth = getLinearDepth( vUv );
			gl_FragColor = vec4( vec3( 1.0 - depth ), 1.0 );

		}`

};

const SSILVBBlendShader = {

	name: 'SSILVBBlendShader',

	uniforms: {
		tDiffuse: { value: null },
		intensity: { value: 1.0 }
	},

	vertexShader: /* glsl */`
		varying vec2 vUv;

		void main() {
			vUv = uv;
			gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );
		}`,

	fragmentShader: /* glsl */`
		uniform float intensity;
		uniform sampler2D tDiffuse;
		varying vec2 vUv;

		void main() {
			vec4 texel = texture2D( tDiffuse, vUv );
			gl_FragColor = vec4(mix(vec3(1.), texel.rgb, intensity), texel.a);
		}`

};


function generateMagicSquareNoise( size = 5 ) {

	const noiseSize = Math.floor( size ) % 2 === 0 ? Math.floor( size ) + 1 : Math.floor( size );
	const magicSquare = generateMagicSquare( noiseSize );
	const noiseSquareSize = magicSquare.length;
	const data = new Uint8Array( noiseSquareSize * 4 );

	for ( let inx = 0; inx < noiseSquareSize; ++ inx ) {

		const iAng = magicSquare[ inx ];
		const angle = ( 2 * Math.PI * iAng ) / noiseSquareSize;
		const randomVec = new Vector3(
			Math.cos( angle ),
			Math.sin( angle ),
			0
		).normalize();
		data[ inx * 4 ] = ( randomVec.x * 0.5 + 0.5 ) * 255;
		data[ inx * 4 + 1 ] = ( randomVec.y * 0.5 + 0.5 ) * 255;
		data[ inx * 4 + 2 ] = 127;
		data[ inx * 4 + 3 ] = 255;

	}

	const noiseTexture = new DataTexture( data, noiseSize, noiseSize );
	noiseTexture.wrapS = RepeatWrapping;
	noiseTexture.wrapT = RepeatWrapping;
	noiseTexture.needsUpdate = true;

	return noiseTexture;

}

function generateMagicSquare( size ) {

	const noiseSize = Math.floor( size ) % 2 === 0 ? Math.floor( size ) + 1 : Math.floor( size );
	const noiseSquareSize = noiseSize * noiseSize;
	const magicSquare = Array( noiseSquareSize ).fill( 0 );
	let i = Math.floor( noiseSize / 2 );
	let j = noiseSize - 1;

	for ( let num = 1; num <= noiseSquareSize; ) {

		if ( i === - 1 && j === noiseSize ) {

			j = noiseSize - 2;
			i = 0;

		} else {

			if ( j === noiseSize ) {

				j = 0;

			}

			if ( i < 0 ) {

				i = noiseSize - 1;

			}

		}

		if ( magicSquare[ i * noiseSize + j ] !== 0 ) {

			j -= 2;
			i ++;
			continue;

		} else {

			magicSquare[ i * noiseSize + j ] = num ++;

		}

		j ++;
		i --;

	}

	return magicSquare;

}


export { generateMagicSquareNoise, SSILVBShader, SSILVBDepthShader, SSILVBBlendShader };
