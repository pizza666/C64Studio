﻿using RetroDevStudio;
using RetroDevStudio.Formats;
using RetroDevStudio.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;



namespace C64Studio.Controls
{
  public partial class ExportAsAssembly : ExportCharscreenFormBase
  {
    public ExportAsAssembly() :
      base( null )
    { 
    }



    public ExportAsAssembly( StudioCore Core ) :
      base( Core )
    {
      InitializeComponent();
    }



    private void checkExportToDataWrap_CheckedChanged( object sender, EventArgs e )
    {
      editWrapByteCount.Enabled = checkExportToDataWrap.Checked;
    }



    public override bool HandleExport( ExportCharsetScreenInfo Info, TextBox EditOutput, DocumentInfo DocInfo )
    {
      var   sb = new StringBuilder();

      if ( checkExportASMAsPetSCII.Checked )
      {
        // pet export only exports chars, no color changes
        bool            isReverse = false;

        sb.Append( ";size " );
        sb.Append( Info.Area.Width );
        sb.Append( "," );
        sb.Append( Info.Area.Height );
        sb.AppendLine();

        for ( int i = Info.Area.Top; i < Info.Area.Bottom; ++i )
        {
          sb.Append( "!pet \"" );
          for ( int x = Info.Area.Left; x < Info.Area.Right; ++x )
          {
            byte newChar = (byte)Info.Charscreen.CharacterAt( x, i );
              
            byte charToAdd = newChar;

            if ( newChar >= 128 )
            {
              isReverse = true;
            }
            else if ( isReverse )
            {
              isReverse = false;
            }
            if ( isReverse )
            {
              charToAdd -= 128;
            }
            if ( ( ConstantData.ScreenCodeToChar[newChar].HasPetSCII )
            &&   ( ConstantData.ScreenCodeToChar[charToAdd].CharValue < 256 ) )
            {
              sb.Append( ConstantData.ScreenCodeToChar[charToAdd].CharValue );
            }
            else
            {
              sb.Append( "\", $" );
              sb.Append( newChar.ToString( "X2" ) );
              sb.Append( ", \"" );
            }
          }
          sb.AppendLine( "\"" );
        }
        EditOutput.Text = sb.ToString();
        return true;
      }

      string screenData = Util.ToASMData( Info.ScreenCharData, checkExportToDataWrap.Checked, GR.Convert.ToI32( editWrapByteCount.Text ), checkExportToDataIncludeRes.Checked ? editPrefix.Text : "", checkExportHex.Checked );
      string colorData  = Util.ToASMData( Info.ScreenColorData, checkExportToDataWrap.Checked, GR.Convert.ToI32( editWrapByteCount.Text ), checkExportToDataIncludeRes.Checked ? editPrefix.Text : "", checkExportHex.Checked );

      sb.Append( ";size " );
      sb.Append( Info.Area.Width );
      sb.Append( "," );
      sb.Append( Info.Area.Height );
      sb.AppendLine();

      switch ( Info.Data )
      {
        case ExportCharsetScreenInfo.ExportData.CHAR_THEN_COLOR:
          sb.Append( ";screen char data" + Environment.NewLine + screenData + Environment.NewLine + ";screen color data" + Environment.NewLine + colorData );
          break;
        case ExportCharsetScreenInfo.ExportData.CHAR_ONLY:
          sb.Append( ";screen char data" + Environment.NewLine + screenData + Environment.NewLine );
          break;
        case ExportCharsetScreenInfo.ExportData.COLOR_ONLY:
          sb.Append( ";screen color data" + Environment.NewLine + colorData );
          break;
        case ExportCharsetScreenInfo.ExportData.COLOR_THEN_CHAR:
          sb.Append( ";screen color data" + Environment.NewLine + colorData + Environment.NewLine + ";screen char data" + Environment.NewLine + screenData );
          break;
        default:
          return false;
      }
      EditOutput.Text = sb.ToString();
      return true;
    }



    private void checkExportToDataIncludeRes_CheckedChanged( object sender, EventArgs e )
    {
      editPrefix.Enabled = checkExportToDataIncludeRes.Checked;
    }



  }
}
