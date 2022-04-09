using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
using MathNet.Numerics;

namespace VRC_OSC_ExternallyTrackedObject
{
    using static Trig;

    internal class MathUtils
    {
        public static void CopyMat34ToOVR(ref Matrix<float> mat, ref HmdMatrix34_t ovrMat)
        {
            ovrMat.m0 = mat[0, 0];
            ovrMat.m1 = mat[0, 1];
            ovrMat.m2 = mat[0, 2];
            ovrMat.m3 = mat[0, 3];
            ovrMat.m4 = mat[1, 0];
            ovrMat.m5 = mat[1, 1];
            ovrMat.m6 = mat[1, 2];
            ovrMat.m7 = mat[1, 3];
            ovrMat.m8 = mat[2, 0];
            ovrMat.m9 = mat[2, 1];
            ovrMat.m10 = mat[2, 2];
            ovrMat.m11 = mat[2, 3];
        }

        public static float Cosf(float input)
        {
            return (float)Cos(input);
        }

        public static float Sinf(float input)
        {
            return (float)Sin(input);
        }

        public static Matrix<float> createTransformMatrix44(float rotX, float rotY, float rotZ, float translateX, float translateY, float translateZ, float scaleX, float scaleY, float scaleZ)
        {

            float[,] data =
            {
                { Cosf(rotY)*Cosf(rotZ) * scaleX,   (Sinf(rotX) * Sinf(rotY) * Cosf(rotZ) - Cosf(rotX) * Sinf(rotZ)) * scaleY,  (Cosf(rotX) * Sinf(rotY) * Cosf(rotZ) + Sinf(rotX) * Sinf(rotZ)) * scaleZ,  translateX },
                { Cosf(rotY)*Sinf(rotZ) * scaleX,   (Sinf(rotX) * Sinf(rotY) * Sinf(rotZ) + Cosf(rotX) * Cosf(rotZ)) * scaleY,  (Cosf(rotX) * Sinf(rotY) * Sinf(rotZ) - Sinf(rotX) * Cosf(rotZ)) * scaleZ,  translateY },
                { -Sinf(rotY) * scaleX, 			Sinf(rotX) * Cosf(rotY) * scaleY,										    Cosf(rotX) * Cosf(rotY) * scaleZ,											translateZ },
                { 0,                                0,                                                                          0,                                                                          1 }
            };

            return Matrix<float>.Build.DenseOfArray(data);
        }

        public static void fillTransformMatrix44(ref Matrix<float> mat, float rotX, float rotY, float rotZ, float translateX, float translateY, float translateZ, float scaleX, float scaleY, float scaleZ)
        {
            mat[0, 0] = Cosf(rotY) * Cosf(rotZ) * scaleX;
            mat[0, 1] = (Sinf(rotX) * Sinf(rotY) * Cosf(rotZ) - Cosf(rotX) * Sinf(rotZ)) * scaleY;
            mat[0, 2] = (Cosf(rotX) * Sinf(rotY) * Cosf(rotZ) + Sinf(rotX) * Sinf(rotZ)) * scaleZ;
            mat[0, 3] = translateX;

            mat[1, 0] = Cosf(rotY) * Sinf(rotZ) * scaleX;
            mat[1, 1] = (Sinf(rotX) * Sinf(rotY) * Sinf(rotZ) + Cosf(rotX) * Cosf(rotZ)) * scaleY;
            mat[1, 2] = (Cosf(rotX) * Sinf(rotY) * Sinf(rotZ) - Sinf(rotX) * Cosf(rotZ)) * scaleZ;
            mat[1, 3] = translateY;

            mat[2, 0] = -Sinf(rotY) * scaleX;
            mat[2, 1] = Sinf(rotX) * Cosf(rotY) * scaleY;
            mat[2, 2] = Cosf(rotX) * Cosf(rotY) * scaleZ;
            mat[2, 3] = translateZ;

            mat[3, 0] = 0;
            mat[3, 1] = 0;
            mat[3, 2] = 0;
            mat[3, 3] = 1;
        }
    }
}
