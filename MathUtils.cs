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

        public static Matrix<float> OVR34ToMat44(ref HmdMatrix34_t ovrMat)
        {
            return Matrix<float>.Build.DenseOfColumnMajor(4, 4, new float[] {
                ovrMat.m0, ovrMat.m4, ovrMat.m8, 0,
                ovrMat.m1, ovrMat.m5, ovrMat.m9, 0,
                ovrMat.m2, ovrMat.m6, ovrMat.m10, 0,
                ovrMat.m3, ovrMat.m7, ovrMat.m11, 1
            });
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

        // ported directly from scipy
        // https://github.com/scipy/scipy/blob/main/scipy/spatial/transform/_rotation.pyx
        public static Vector<float> extractRotationsFromMatrix(Matrix<float> mat33)
        {
            // turn rotation matrix into quaternion first
            var quat = Mat33toQuat(mat33);

            // turn quaternion back into matrix
            var mat = QuatToMat33(quat);


        }

        public static Vector<float> Mat33ToEulerYXZ()
        {
            // TODO
        }

        public static Vector<float> Mat33toQuat(Matrix<float> mat33)
        {
            var quat = Vector<float>.Build.Dense(4);

            int i, j, k;

            float[] decision = new float[4] { 0, 0, 0, 0 };

            decision[0] = mat33[0, 0];
            decision[1] = mat33[1, 1];
            decision[2] = mat33[2, 2];
            decision[3] = mat33[0, 0] + mat33[1, 1] + mat33[2, 2];

            int choice = argmax(decision);

            if (choice != 3)
            {
                i = choice;
                j = (i + 1) % 3;
                k = (j + 1) % 3;

                quat[i] = 1 - decision[3] + 2 * mat33[i, i];
                quat[j] = mat33[j, i] + mat33[i, j];
                quat[k] = mat33[k, i] + mat33[i, k];
                quat[3] = mat33[k, j] - mat33[j, k];
            }
            else
            {
                quat[0] = mat33[2, 1] - mat33[1, 2];
                quat[1] = mat33[0, 2] - mat33[2, 0];
                quat[2] = mat33[1, 0] - mat33[0, 1];
                quat[3] = 1 + decision[3];
            }

            return quat.Normalize(2);
        }

        public static Matrix<float> QuatToMat33(Vector<float> quat)
        {
            float x2 = quat[0] * quat[0];
            float y2 = quat[1] * quat[1];
            float z2 = quat[2] * quat[2];
            float w2 = quat[3] * quat[3];

            float xy = quat[0] * quat[1];
            float zw = quat[2] * quat[3];
            float xz = quat[0] * quat[2];
            float yw = quat[1] * quat[3];
            float yz = quat[1] * quat[2];
            float xw = quat[0] * quat[3];

            return Matrix<float>.Build.DenseOfColumnMajor(3, 3, new float[]
            {
                x2 - y2 - z2 + w2, // 0, 0
                2 * (xy - zw), // 0, 1
                2 * (xz + yw), // 0, 2
                2 * (xy + zw), // 1, 0
                - x2 + y2 - z2 + w2, // 1, 1
                2 * (yz - xw), // 1, 2
                2 * (xz - yw), // 2, 0
                2 * (yz + xw), // 2, 1
                - x2 - y2 + z2 + w2, // 2, 2
            });
        }

        public static int argmax(float[] input)
        {
            int hi = 0;
            float hv = Single.NegativeInfinity;

            for (int i = 0; i < input.Length; ++i)
            {
                if (input[i] > hv)
                {
                    hv = input[i];
                    hi = i;
                }
            }

            return hi;
        }
    }
}
