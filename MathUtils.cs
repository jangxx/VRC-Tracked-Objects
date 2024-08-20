using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
using MathNet.Numerics;
using MathNet.Spatial.Euclidean;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VRC_OSC_ExternallyTrackedObject
{
    using static Trig;

    internal class MathUtils
    {
        public static void CopyMat34ToOVR(ref Matrix<double> mat, ref HmdMatrix34_t ovrMat)
        {
            ovrMat.m0 = (float)mat[0, 0];
            ovrMat.m1 = (float)mat[0, 1];
            ovrMat.m2 = (float)mat[0, 2];
            ovrMat.m3 = (float)mat[0, 3];
            ovrMat.m4 = (float)mat[1, 0];
            ovrMat.m5 = (float)mat[1, 1];
            ovrMat.m6 = (float)mat[1, 2];
            ovrMat.m7 = (float)mat[1, 3];
            ovrMat.m8 = (float)mat[2, 0];
            ovrMat.m9 = (float)mat[2, 1];
            ovrMat.m10 = (float)mat[2, 2];
            ovrMat.m11 = (float)mat[2, 3];
        }

        public static Matrix<double> OVR34ToMat44(ref HmdMatrix34_t ovrMat)
        {
            return Matrix<double>.Build.DenseOfColumnMajor(4, 4, new double[] {
                ovrMat.m0, ovrMat.m4, ovrMat.m8, 0,
                ovrMat.m1, ovrMat.m5, ovrMat.m9, 0,
                ovrMat.m2, ovrMat.m6, ovrMat.m10, 0,
                ovrMat.m3, ovrMat.m7, ovrMat.m11, 1
            });
        }

        //public static Matrix<double> createStableTransformMatrix44(double rotX, double rotY, double rotZ, double translateX, double translateY, double translateZ, double scaleX, double scaleY, double scaleZ)
        //{

        //    double[,] data =
        //    {
        //        { Cos(rotY)*Cos(rotZ) * scaleX,   (Sin(rotX) * Sin(rotY) * Cos(rotZ) - Cos(rotX) * Sin(rotZ)) * scaleY,  (Cos(rotX) * Sin(rotY) * Cos(rotZ) + Sin(rotX) * Sin(rotZ)) * scaleZ,  translateX },
        //        { Cos(rotY)*Sin(rotZ) * scaleX,   (Sin(rotX) * Sin(rotY) * Sin(rotZ) + Cos(rotX) * Cos(rotZ)) * scaleY,  (Cos(rotX) * Sin(rotY) * Sin(rotZ) - Sin(rotX) * Cos(rotZ)) * scaleZ,  translateY },
        //        { -Sin(rotY) * scaleX,            Sin(rotX) * Cos(rotY) * scaleY,                                        Cos(rotX) * Cos(rotY) * scaleZ,                                        translateZ },
        //        { 0,                              0,                                                                     0,                                                                     1 }
        //    };

        //    return Matrix<double>.Build.DenseOfArray(data);
        //}

        public static Matrix<double> createTransformMatrix44(double rotX, double rotY, double rotZ, double translateX, double translateY, double translateZ, double scaleX, double scaleY, double scaleZ)
        {
            // copied from https://github.com/mrdoob/three.js/blob/dev/src/math/Matrix4.js

            double a = Cos(rotX);
            double b = Sin(rotX);
            double c = Cos(rotY);
            double d = Sin(rotY);
            double e = Cos(rotZ);
            double f = Sin(rotZ);

            //double[,] data =
            //{
            //    { ca*cb * scaleX, (ca*sb*sg - cg*sa) * scaleY, (sa*sg + ca*cg*sb) * scaleZ,  translateX },
            //    { cb*sa * scaleX, (ca*cg + sa*sb*sg) * scaleY, (cg*sa*sb - ca*sg) * scaleZ,  translateY },
            //    { -sb * scaleX,   cb*sg * scaleY,              cb*cg * scaleZ,               translateZ },
            //    { 0,  0,  0,  1 }
            //};

            double[,] data =
            {
                { c*e * scaleX,           -c*f * scaleY,         d * scaleZ,     translateX },
                { (a*f + b*e*d) * scaleX, (a*e - b*f*d) * scaleY, -b*c * scaleZ, translateY },
                { (b*f - a*e*d) * scaleX, (b*e + a*f*d) * scaleY, a*c * scaleZ,  translateZ },
                { 0,  0,  0,  1 },
            };

            return Matrix<double>.Build.DenseOfArray(data);
        }

        public static void fillTransformMatrix44(ref Matrix<double> mat, double rotX, double rotY, double rotZ, double translateX, double translateY, double translateZ, double scaleX, double scaleY, double scaleZ)
        {
            double a = Cos(rotX);
            double b = Sin(rotX);
            double c = Cos(rotY);
            double d = Sin(rotY);
            double e = Cos(rotZ);
            double f = Sin(rotZ);

            mat[0, 0] = c * e * scaleX;
            mat[0, 1] = -c * f * scaleY;
            mat[0, 2] = d * scaleZ;
            mat[0, 3] = translateX;

            mat[1, 0] = (a * f + b * e * d) * scaleX;
            mat[1, 1] = (a * e - b * f * d) * scaleY;
            mat[1, 2] = -b * c * scaleZ;
            mat[1, 3] = translateY;

            mat[2, 0] = (b * f - a * e * d) * scaleX;
            mat[2, 1] = (b * e + a * f * d) * scaleY;
            mat[2, 2] = a * c * scaleZ;
            mat[2, 3] = translateZ;

            mat[3, 0] = 0;
            mat[3, 1] = 0;
            mat[3, 2] = 0;
            mat[3, 3] = 1;
        }

        public static Vector<double> extractRotationsFromMatrix(Matrix<double> mat33)
        {
            // create XYZ Euler angles
            // copied from https://github.com/mrdoob/three.js/blob/dev/src/math/Euler.js
            // and extended to properly deal with scaled matrices

            double scaleX = mat33.Column(0).L2Norm();
            double scaleY = mat33.Column(1).L2Norm();
            double scaleZ = mat33.Column(2).L2Norm();

            double m13 = mat33[0, 2] / scaleZ;
            double m23 = mat33[1, 2] / scaleZ;
            double m33 = mat33[2, 2] / scaleZ;
            double m12 = mat33[0, 1] / scaleY;
            double m11 = mat33[0, 0] / scaleX;
            double m32 = mat33[2, 1] / scaleY;
            double m22 = mat33[1, 1] / scaleY;

            double y = Math.Asin(Math.Clamp(m13, -1.0, 1.0));
            double x, z;

            if (Math.Abs(m13) < 0.9999999)
            {
                x = Math.Atan2(-m23, m33);
                z = Math.Atan2(-m12, m11);
            }
            else
            {
                x = Math.Atan2(-m32, m22);
                z = 0.0;
            }

            // return the result as a numerics vector
            return Vector<double>.Build.DenseOfArray(new double[]{ x, y, z });
        }

        public static Vector<double> extractRotationsFromMatrixZYX(Matrix<double> mat33)
        {
            // I have zero idea why this works, but the code below doesn't, since they should both do exactly the same thing - get ZYX euler angles from a matrix

            // turn rotation matrix into quaternion first
            var quat = Mat33toQuat(mat33);

            // turn quaternion into euler angles
            var euler = quat.ToEulerAngles();

            // return the result as a numerics vector
            return Vector<double>.Build.DenseOfArray(new double[]
            {
                euler.Alpha.Radians,
                euler.Beta.Radians,
                euler.Gamma.Radians
            });

            //// copied from https://github.com/mrdoob/three.js/blob/dev/src/math/Euler.js
            //// and extended to properly deal with scaled matrices

            //double scaleX = mat33.Column(0).L2Norm();
            //double scaleY = mat33.Column(1).L2Norm();
            //double scaleZ = mat33.Column(2).L2Norm();

            //double m31 = mat33[2, 0] / scaleX;
            //double m33 = mat33[2, 2] / scaleZ;
            //double m12 = mat33[0, 1] / scaleY;
            //double m11 = mat33[0, 0] / scaleX;
            //double m32 = mat33[2, 1] / scaleY;
            //double m22 = mat33[1, 1] / scaleY;
            //double m21 = mat33[1, 0] / scaleX;

            //double y = Math.Asin(Math.Clamp(m31, -1.0, 1.0));
            //double x, z;

            //if (Math.Abs(m31) < 0.9999999)
            //{
            //    x = Math.Atan2(m32, m33);
            //    z = Math.Atan2(m21, m11);
            //}
            //else
            //{
            //    x = 0.0;
            //    z = Math.Atan2(-m12, m22);
            //}

            //// return the result as a numerics vector
            //return Vector<double>.Build.DenseOfArray(new double[] { x, y, z });
        }

        public static Vector<double> extractRotationsFromMatrix44(Matrix<double> mat44)
        {
            return extractRotationsFromMatrix(mat44.SubMatrix(0, 3, 0, 3));
        }

        public static Vector<double> extractTranslationFromMatrix44(Matrix<double> mat44)
        {
            return mat44.Column(3);
        }

        public static Vector<double> extractScaleFromMatrix44(Matrix<double> mat44)
        {
            return Vector<double>.Build.DenseOfArray(new double[]
            {
                mat44.Column(0).L2Norm(),
                mat44.Column(1).L2Norm(),
                mat44.Column(2).L2Norm(),
            });
        }

        // ported directly from https://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/
        public static Quaternion Mat33toQuat(Matrix<double> mat33)
        {

            double tr = mat33[0, 0] + mat33[1, 1] + mat33[2, 2];
            double qx, qy, qz, qw;

            if (tr > 0)
            {
                double S = Math.Sqrt(tr + 1.0f) * 2; // S=4*qw 
                qw = 0.25 * S;
                qx = (mat33[2, 1] - mat33[1, 2]) / S;
                qy = (mat33[0, 2] - mat33[2, 0]) / S;
                qz = (mat33[1, 0] - mat33[0, 1]) / S;
            }
            else if ((mat33[0, 0] > mat33[1, 1]) & (mat33[0, 0] > mat33[2, 2]))
            {
                double S = Math.Sqrt(1.0 + mat33[0, 0] - mat33[1, 1] - mat33[2, 2]) * 2; // S=4*qx 
                qw = (mat33[2, 1] - mat33[1, 2]) / S;
                qx = 0.25 * S;
                qy = (mat33[0, 1] + mat33[1, 0]) / S;
                qz = (mat33[0, 2] + mat33[2, 0]) / S;
            }
            else if (mat33[1, 1] > mat33[2, 2])
            {
                double S = Math.Sqrt(1.0 + mat33[1, 1] - mat33[0, 0] - mat33[2, 2]) * 2; // S=4*qy
                qw = (mat33[0, 2] - mat33[2, 0]) / S;
                qx = (mat33[0, 1] + mat33[1, 0]) / S;
                qy = 0.25 * S;
                qz = (mat33[1, 2] + mat33[2, 1]) / S;
            }
            else
            {
                double S = Math.Sqrt(1.0 + mat33[2, 2] - mat33[0, 0] - mat33[1, 1]) * 2; // S=4*qz
                qw = (mat33[1, 0] - mat33[0, 1]) / S;
                qx = (mat33[0, 2] + mat33[2, 0]) / S;
                qy = (mat33[1, 2] + mat33[2, 1]) / S;
                qz = 0.25 * S;
            }

            return new Quaternion(qw, qx, qy, qz).Normalized;
        }

        public static Matrix<double> QuatToMat33(Vector<double> quat)
        {
            double x2 = quat[0] * quat[0];
            double y2 = quat[1] * quat[1];
            double z2 = quat[2] * quat[2];
            double w2 = quat[3] * quat[3];

            double xy = quat[0] * quat[1];
            double zw = quat[2] * quat[3];
            double xz = quat[0] * quat[2];
            double yw = quat[1] * quat[3];
            double yz = quat[1] * quat[2];
            double xw = quat[0] * quat[3];

            return Matrix<double>.Build.DenseOfColumnMajor(3, 3, new double[]
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

            for (int i = 1; i < input.Length; ++i)
            {
                if (input[i] > input[hi])
                {
                    hi = i;
                }
            }

            return hi;
        }
    }
}
