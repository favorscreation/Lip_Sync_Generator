using OpenCvSharp;
using System;
using System.Diagnostics;

namespace Lip_Sync_Generator_2
{
    internal class MatFunction
    {
        /// <summary>
        ///         //透明ピクセルを置換
        /// </summary>
        /// <param name="mat"></param>
        static public void Transparent_replacement(Mat mat, byte R, byte G, byte B)
        {
            //ポインタによるアクセス
            unsafe
            {
                byte* b = mat.DataPointer;
                for (int i = 0; i < mat.Height; i++)
                {
                    for (int j = 0; j < mat.Width; j++)
                    {
                        if (b[3] == 0)
                        {
                            b[0] = B;   //B
                            b[1] = G;   //G
                            b[2] = R;   //R
                            b[3] = 255; //A
                        }
                        b = b + 4;
                    }
                }
            }
        }

        /// <summary>
        /// 透明ピクセルを黒に置換　マスク処理用
        /// </summary>
        /// <param name="mat"></param>
        static public void Transparent_replacement_ToBlack(Mat mat)
        {
            //ポインタによるアクセス
            unsafe
            {
                byte* b = mat.DataPointer;
                for (int i = 0; i < mat.Height; i++)
                {
                    for (int j = 0; j < mat.Width; j++)
                    {
                        if (b[3] == 0)
                        {
                            b[0] = 0; //B
                            b[1] = 0; //G
                            b[2] = 0; //R
                            b[3] = 0; //A
                        }
                        b = b + 4;
                    }
                }
            }
        }

        /// <summary>
        /// 透過画像を重ねる 同じサイズである必要がある
        /// </summary>
        /// <param name="src"></param>
        /// <param name="add"></param>
        static public void TransparentComposition(Mat src, Mat add)
        {

            //シングル
            /*
            unsafe
            {
                byte* src_b = src.DataPointer;
                byte* add_b = add.DataPointer;
                float weight;
                for (int i = 0; i < src.Height; i++)
                {
                    for (int j = 0; j < src.Width; j++)
                    {
                        if (add_b[3] != 0)
                        {
                            weight = src_b[3] / 255;//0-1
                            src_b[0] = (byte)(add_b[0] * weight + src_b[0] * (1 - weight)); //B
                            src_b[1] = (byte)(add_b[1] * weight + src_b[1] * (1 - weight)); //G
                            src_b[2] = (byte)(add_b[2] * weight + src_b[2] * (1 - weight)); //R
                            src_b[3] = (byte)(add_b[3] * weight + src_b[3] * (1 - weight)); //A
                        }

                        src_b = src_b + 4;
                        add_b = add_b + 4;
                    }
                }

                Debug.WriteLine(src_b - src.DataPointer);

            }
            */


            //並列化
            unsafe
            {
                int Num = src.Height * src.Width;
                int d = Environment.ProcessorCount;
                Parallel.For(0, d, x =>
                {
                    float weight;
                    byte* src_b = src.DataPointer;
                    byte* add_b = add.DataPointer;

                    src_b = src_b + (Num / d * x * 4);
                    add_b = add_b + (Num / d * x * 4);

                    for (int i = 0; i < Num / d; i++)
                    {
                        if (add_b[3] != 0)
                        {
                            weight = (float)add_b[3] / 255;//0-1
                            src_b[0] = (byte)((add_b[0] * weight) + (src_b[0] * (1 - weight))); //B
                            src_b[1] = (byte)((add_b[1] * weight) + (src_b[1] * (1 - weight))); //G
                            src_b[2] = (byte)((add_b[2] * weight) + (src_b[2] * (1 - weight))); //R
                            src_b[3] = (byte)((add_b[3] * weight) + (src_b[3] * (1 - weight))); //A
                        }
                        src_b = src_b + 4;
                        add_b = add_b + 4;
                    }
                });
            }
        }
    }
}
