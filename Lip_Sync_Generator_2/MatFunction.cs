using OpenCvSharp;
using System;
using System.Threading.Tasks;

namespace Lip_Sync_Generator_2
{
    internal class MatFunction
    {
         /// <summary>
        /// 透明ピクセルを指定色で置換する
        /// </summary>
        /// <param name="mat">対象のMatオブジェクト</param>
        /// <param name="R">置換する赤色の値</param>
        /// <param name="G">置換する緑色の値</param>
        /// <param name="B">置換する青色の値</param>
        static public void Transparent_replacement(Mat mat, byte R, byte G, byte B)
        {
            unsafe
            {
                byte* b = mat.DataPointer;
                for (int i = 0; i < mat.Height; i++)
                {
                    for (int j = 0; j < mat.Width; j++)
                    {
                        if (b[3] == 0) // アルファ値が0（透明）の場合
                        {
                            b[0] = B;   //B
                            b[1] = G;   //G
                            b[2] = R;   //R
                            b[3] = 255; //A (不透明にする)
                        }
                        b += 4;
                    }
                }
            }
        }

        /// <summary>
        /// 透明ピクセルを黒色で置換する (マスク処理用)
        /// </summary>
        /// <param name="mat">対象のMatオブジェクト</param>
        static public void Transparent_replacement_ToBlack(Mat mat)
        {
            unsafe
            {
                byte* b = mat.DataPointer;
                for (int i = 0; i < mat.Height; i++)
                {
                    for (int j = 0; j < mat.Width; j++)
                    {
                        if (b[3] == 0) // アルファ値が0（透明）の場合
                        {
                            b[0] = 0; //B
                            b[1] = 0; //G
                            b[2] = 0; //R
                            b[3] = 0; //A (透明にする)
                        }
                        b += 4;
                    }
                }
            }
        }

        /// <summary>
        /// 透過画像を重ね合わせる（アルファブレンド）
        /// </summary>
        /// <param name="src">合成先のMatオブジェクト</param>
        /// <param name="add">合成するMatオブジェクト</param>
        static public void TransparentComposition(Mat src, Mat add)
        {
            if (src.Size() != add.Size())
            {
                throw new ArgumentException("画像のサイズが異なります。");
            }

            unsafe
            {
                int numPixels = src.Height * src.Width;
                Cv2.ParallelLoopBody((index, total) =>
                {
                    int start = (int)((long)index * numPixels / total);
                    int end = (int)((long)(index + 1) * numPixels / total);

                    byte* src_b = src.DataPointer + start * 4;
                    byte* add_b = add.DataPointer + start * 4;

                    for (int i = start; i < end; i++)
                    {
                        float alpha = (float)add_b[3] / 255.0f; // 合成する画像のアルファ値を0-1の範囲に変換

                         if(alpha > 0)
                        {
                           // アルファブレンド処理
                           src_b[0] = (byte)((add_b[0] * alpha) + (src_b[0] * (1 - alpha))); // B
                           src_b[1] = (byte)((add_b[1] * alpha) + (src_b[1] * (1 - alpha))); // G
                           src_b[2] = (byte)((add_b[2] * alpha) + (src_b[2] * (1 - alpha))); // R

                           // 合成後のアルファ値を不透明に設定 (不透明合成)
                           src_b[3] = 255;
                       }

                        src_b += 4;
                        add_b += 4;
                    }
                });
            }
        }
    }
}
