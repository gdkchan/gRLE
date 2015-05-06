using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Forms;

namespace gRLE
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlgOpen = new OpenFileDialog();
            dlgOpen.Filter = "Imagem RLE|*.rle";
            if (dlgOpen.ShowDialog() == System.Windows.Forms.DialogResult.OK && File.Exists(dlgOpen.FileName))
            {
                byte[] data = RLEDecompress(new FileStream(dlgOpen.FileName, FileMode.Open));

                int width = 512;
                int.TryParse(textBox1.Text, out width);

                Bitmap img = new Bitmap(width, (data.Length / width) / 2);
                for (int y = 0; y < img.Height; y++)
                {
                    for (int x = 0; x < img.Width; x++)
                    {
                        int o = ((y * img.Width) + x) * 2;

                        ushort val = BitConverter.ToUInt16(data, o);
                        byte B = (byte)((val >> 10) & 0x1f);
                        byte G = (byte)((val >> 5) & 0x1f);
                        byte R = (byte)((val) & 0x1f);

                        R = (byte)((R << 3) | (R >> 2));
                        G = (byte)((G << 3) | (G >> 2));
                        B = (byte)((B << 3) | (B >> 2));

                        img.SetPixel(x, y, Color.FromArgb(R, G, B));
                    }
                }

                pictureBox1.Image = img;
                button2.Enabled = true;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.Filter = "Imagem|*.png";
            if (saveDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pictureBox1.Image.Save(saveDlg.FileName);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlgOpen = new OpenFileDialog();
            dlgOpen.Filter = "Imagem|*.png";
            if (dlgOpen.ShowDialog() == System.Windows.Forms.DialogResult.OK && File.Exists(dlgOpen.FileName))
            {
                pictureBox1.Image = new Bitmap(dlgOpen.FileName);
                button4.Enabled = true;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.Filter = "Imagem RLE|*.rle";
            if (saveDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Bitmap img = new Bitmap(pictureBox1.Image);

                MemoryStream rawImage = new MemoryStream();
                BinaryWriter writer = new BinaryWriter(rawImage);
                for (int y = 0; y < img.Height; y++)
                {
                    for (int x = 0; x < img.Width; x++)
                    {
                        Color color = img.GetPixel(x, y);

                        ushort val = (ushort)(((color.R & 0xf8) >> 3) | ((color.G & 0xf8) << 2) | ((color.B & 0xf8) << 7));
                        writer.Write(val);
                    }
                }
                rawImage.Seek(0, SeekOrigin.Begin);

                byte[] RLEData = RLECompress(rawImage);
                File.WriteAllBytes(saveDlg.FileName, RLEData);
            }
        }

        private byte[] RLEDecompress(Stream input)
        {
            BinaryReader reader = new BinaryReader(input);
            MemoryStream output = new MemoryStream();
            String signature = null;
            for (int i = 0; i < 8; i++)
            {
                signature += Convert.ToChar(reader.ReadByte());
            }
            if (signature != "_RLE_16_")
            {
                MessageBox.Show("Arquivo inválido!", "Erro!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            uint decompressedLength = reader.ReadUInt32();
            while (input.Position < input.Length)
            {
                ushort header = reader.ReadUInt16();
                ushort length = (ushort)(header & 0x7fff);
                if (length == 0) break;
     
                if ((header & 0x8000) != 0)
                {
                    ushort data = reader.ReadUInt16();
                    for (int i = 0; i < length; i++)
                    {
                        output.WriteByte((byte)(data & 0xff));
                        output.WriteByte((byte)(data >> 8));
                    }
                }
                else
                {
                    for (int i = 0; i < length; i++)
                    {
                        ushort data = reader.ReadUInt16();
                        output.WriteByte((byte)(data & 0xff));
                        output.WriteByte((byte)(data >> 8));
                    }
                }

            }

            input.Close();
            return output.ToArray();
        }

        private byte[] RLECompress(Stream input)
        {
            BinaryReader reader = new BinaryReader(input);

            MemoryStream output = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(output);
            writer.Write(Encoding.ASCII.GetBytes("_RLE_16_"));
            writer.Write((uint)input.Length); //O jogo não usa isso, e sim o EOF

            bool EOF = false;
            while (input.Position < input.Length - 1)
            {
                ushort data = reader.ReadUInt16();
                if (EOF || input.Position >= input.Length - 1)
                {
                    writer.Write((ushort)1);
                    writer.Write(data);
                    break;
                }
                ushort length = 0;

                while (input.Position < input.Length - 1 && reader.ReadUInt16() == data && length < 0x7ffe) length++;
                if (input.Position >= input.Length - 1) EOF = true;
                input.Seek(-2, SeekOrigin.Current);
                if (length > 0)
                {
                    writer.Write((ushort)(0x8000 | (length + 1)));
                    writer.Write(data);
                    length = 0;
                }
                else
                {
                    bool different = true;
                    long headerPosition = output.Position;
                    writer.Write((ushort)0); //Reserva espaço do header
                    while (different && length < 0x7fff && input.Position < input.Length - 1)
                    {
                        ushort nextData = reader.ReadUInt16();
                        different = data != nextData;
                        writer.Write(data);
                        length++;
                        data = nextData;
                    }
                    if (input.Position >= input.Length - 1) EOF = true;
                    input.Seek(-2, SeekOrigin.Current);
                    long position = output.Position;
                    output.Seek(headerPosition, SeekOrigin.Begin);
                    writer.Write((ushort)length);
                    output.Seek(position, SeekOrigin.Begin);
                }
            }

            writer.Write((ushort)0); //EOF

            input.Close();
            return output.ToArray();
        }
    }
}
