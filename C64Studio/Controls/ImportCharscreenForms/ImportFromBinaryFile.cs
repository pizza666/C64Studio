﻿using C64Studio.Formats;
using C64Studio.Types;
using GR.Memory;
using RetroDevStudio;
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
using static C64Studio.BaseDocument;

namespace C64Studio.Controls
{
  public partial class ImportFromBinaryFile : ImportCharscreenFormBase
  {
    public ImportFromBinaryFile() :
      base( null )
    { 
    }



    public ImportFromBinaryFile( StudioCore Core ) :
      base( Core )
    {
      InitializeComponent();
    }



    public override bool HandleImport( CharsetScreenProject CharScreen, CharsetScreenEditor Editor )
    {
      string filename;

      if ( Editor.OpenFile( "Open Charpad project, Marq's PETSCII editor files or binary data", Constants.FILEFILTER_CHARSET_CHARPAD + Constants.FILEFILTER_MARQS_PETSCII + Constants.FILEFILTER_ALL, out filename ) )
      {
        if ( System.IO.Path.GetExtension( filename ).ToUpper() == ".CTM" )
        {
          // a charpad project file
          GR.Memory.ByteBuffer projectFile = GR.IO.File.ReadAllBytes( filename );

          Formats.CharpadProject    cpProject = new C64Studio.Formats.CharpadProject();
          if ( !cpProject.LoadFromFile( projectFile ) )
          {
            return false;
          }

          CharScreen.CharSet.Colors.BackgroundColor = cpProject.BackgroundColor;
          CharScreen.CharSet.Colors.MultiColor1 = cpProject.MultiColor1;
          CharScreen.CharSet.Colors.MultiColor2 = cpProject.MultiColor2;
          CharScreen.CharSet.Colors.BGColor4 = cpProject.BackgroundColor4;

          int maxChars = cpProject.NumChars;
          if ( maxChars > CharScreen.CharSet.TotalNumberOfCharacters )
          {
            maxChars = CharScreen.CharSet.TotalNumberOfCharacters;
          }

          CharScreen.CharSet.ExportNumCharacters = maxChars;
          for ( int charIndex = 0; charIndex < CharScreen.CharSet.ExportNumCharacters; ++charIndex )
          {
            CharScreen.CharSet.Characters[charIndex].Tile.Data = cpProject.Characters[charIndex].Data;
            CharScreen.CharSet.Characters[charIndex].Tile.CustomColor = cpProject.Characters[charIndex].Color;

            Editor.RebuildCharImage( charIndex );
          }

          // import tiles
          var mapProject = new MapProject();
          mapProject.MultiColor1 = cpProject.MultiColor1;
          mapProject.MultiColor2 = cpProject.MultiColor2;

          for ( int i = 0; i < cpProject.NumTiles; ++i )
          {
            Formats.MapProject.Tile tile = new Formats.MapProject.Tile();

            tile.Name = "Tile " + ( i + 1 ).ToString();
            tile.Chars.Resize( cpProject.TileWidth, cpProject.TileHeight );
            tile.Index = i;

            for ( int y = 0; y < tile.Chars.Height; ++y )
            {
              for ( int x = 0; x < tile.Chars.Width; ++x )
              {
                tile.Chars[x, y].Character = (byte)cpProject.Tiles[i].CharData.UInt16At( 2 * ( x + y * tile.Chars.Width ) );
                tile.Chars[x, y].Color = cpProject.Tiles[i].ColorData.ByteAt( x + y * tile.Chars.Width );
              }
            }
            mapProject.Tiles.Add( tile );
          }

          var map = new Formats.MapProject.Map();
          map.Tiles.Resize( cpProject.MapWidth, cpProject.MapHeight );
          for ( int j = 0; j < cpProject.MapHeight; ++j )
          {
            for ( int i = 0; i < cpProject.MapWidth; ++i )
            {
              map.Tiles[i, j] = cpProject.MapData.ByteAt( i + j * cpProject.MapWidth );
            }
          }
          map.TileSpacingX = cpProject.TileWidth;
          map.TileSpacingY = cpProject.TileHeight;
          mapProject.Maps.Add( map );

          Editor.comboBackground.SelectedIndex = mapProject.Charset.Colors.BackgroundColor;
          Editor.comboMulticolor1.SelectedIndex = mapProject.Charset.Colors.MultiColor1;
          Editor.comboMulticolor2.SelectedIndex = mapProject.Charset.Colors.MultiColor2;
          Editor.comboBGColor4.SelectedIndex = mapProject.Charset.Colors.BGColor4;
          Editor.comboCharsetMode.SelectedIndex = (int)cpProject.DisplayModeFile;

          GR.Memory.ByteBuffer      charData = new GR.Memory.ByteBuffer( (uint)( map.Tiles.Width * map.TileSpacingX * map.Tiles.Height * map.TileSpacingY ) );
          GR.Memory.ByteBuffer      colorData = new GR.Memory.ByteBuffer( (uint)( map.Tiles.Width * map.TileSpacingX * map.Tiles.Height * map.TileSpacingY ) );

          for ( int y = 0; y < map.Tiles.Height; ++y )
          {
            for ( int x = 0; x < map.Tiles.Width; ++x )
            {
              int tileIndex = map.Tiles[x, y];
              if ( tileIndex < mapProject.Tiles.Count )
              {
                // a real tile
                var tile = mapProject.Tiles[tileIndex];

                for ( int j = 0; j < tile.Chars.Height; ++j )
                {
                  for ( int i = 0; i < tile.Chars.Width; ++i )
                  {
                    charData.SetU8At( x * map.TileSpacingX + i + ( y * map.TileSpacingY + j ) * ( map.Tiles.Width * map.TileSpacingX ), tile.Chars[i, j].Character );
                    colorData.SetU8At( x * map.TileSpacingX + i + ( y * map.TileSpacingY + j ) * ( map.Tiles.Width * map.TileSpacingX ), tile.Chars[i, j].Color );
                  }
                }
              }
            }
          }

          if ( cpProject.MapColorData != null )
          {
            // this charpad project has alternative color data
            for ( int i = 0; i < cpProject.MapHeight; ++i )
            {
              for ( int j = 0; j < cpProject.MapWidth; ++j )
              {
                colorData.SetU8At( j + i * cpProject.MapWidth, cpProject.MapColorData.ByteAt( j + i * cpProject.MapWidth ) );
              }
            }
          }

          Editor.ImportFromData( map.TileSpacingX * map.Tiles.Width,
                          map.TileSpacingY * map.Tiles.Height,
                          charData, colorData, CharScreen.CharSet );
        }
        else if ( System.IO.Path.GetExtension( filename ).ToUpper() == ".C" )
        {
          string cData = GR.IO.File.ReadAllText( filename );
          if ( !string.IsNullOrEmpty( cData ) )
          {
            int     dataStart = cData.IndexOf( '{' );
            if ( dataStart == -1 )
            {
              return false;
            }
            int     dataEnd = cData.IndexOf( '}', dataStart );
            if ( dataEnd == -1 )
            {
              return false;
            }
            string  actualData = cData.Substring( dataStart + 1, dataEnd - dataStart - 2 );

            var screenData = new ByteBuffer();

            var dataLines = actualData.Split( '\n' );
            for ( int i = 0; i < dataLines.Length; ++i )
            {
              var dataLine = dataLines[i].Trim();
              if ( dataLine.StartsWith( "//" ) )
              {
                continue;
              }
              int     pos = 0;
              int     commaPos = -1;

              while ( pos < dataLine.Length )
              {
                commaPos = dataLine.IndexOf( ',', pos );
                if ( commaPos == -1 )
                {
                  // end of line
                  byte    byteValue = GR.Convert.ToU8( dataLine.Substring( pos ) );

                  screenData.AppendU8( byteValue );
                  break;
                }
                else
                {
                  byte    byteValue = GR.Convert.ToU8( dataLine.Substring( pos, commaPos - pos ) );

                  screenData.AppendU8( byteValue );
                  pos = commaPos + 1;
                }
              }

              // border and BG first
              CharScreen.CharSet.Colors.BackgroundColor = screenData.ByteAt( 1 );
              screenData = screenData.SubBuffer( 2 );

              Editor.ImportFromData( screenData );
            }
          }
        }
        else
        {
          GR.Memory.ByteBuffer data = GR.IO.File.ReadAllBytes( filename );

          Editor.ImportFromData( data );
        }
      }

      return true;
    }



  }
}
