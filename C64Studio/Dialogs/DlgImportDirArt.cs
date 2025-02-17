﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace C64Studio
{
  public partial class DlgImportDirArt : Form
  {
    public GR.Memory.ByteBuffer         ResultingData = new GR.Memory.ByteBuffer();



    public DlgImportDirArt( StudioCore Core )
    {
      InitializeComponent();

      Core.Theming.ApplyTheme( this );
    }



    private void btnOK_Click( object sender, EventArgs e )
    {
      DialogResult = DialogResult.OK;
      Close();
    }



    private void editASMDirArt_TextChanged( object sender, EventArgs e )
    {
      Parser.ASMFileParser asmParser = new C64Studio.Parser.ASMFileParser();

      Parser.CompileConfig config = new Parser.CompileConfig();
      config.TargetType = Types.CompileTargetType.PLAIN;
      config.OutputFile = "temp.bin";
      config.Assembler = Types.AssemblerType.C64_STUDIO;

      string    temp = "* = $0801\n" + editASMDirArt.Text;
      if ( ( asmParser.Parse( temp, null, config, null ) )
      &&   ( asmParser.Assemble( config ) ) )
      {
        GR.Memory.ByteBuffer data = asmParser.AssembledOutput.Assembly;

        ResultingData = data;

        labelASMInfo.Text = "Data is valid";
        labelASMInfo.ForeColor = System.Drawing.SystemColors.ControlText;
      }
      else
      {
        labelASMInfo.Text = "Invalid ASM Data (expect !byte statements)";
        labelASMInfo.ForeColor = System.Drawing.Color.Red;
      }
    }



  }
}
