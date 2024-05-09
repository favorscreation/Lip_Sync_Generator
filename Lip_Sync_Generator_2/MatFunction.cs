using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

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
                            b[0] = B; //B
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
    }
}
