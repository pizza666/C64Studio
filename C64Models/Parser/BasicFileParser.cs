﻿using System;
using System.Collections.Generic;
using System.Text;
using GR.Memory;

namespace C64Studio.Parser
{
  public class BasicFileParser : ParserBase
  {
    public enum BasicVersion
    {
      V1_0,   // Version 1.0	Noch recht fehlerbehaftet, erste Versionen im PET 2001
      V2_0,   // August  Version 2.0	Fehlerkorrekturen, eingebaut in weitere Versionen des PET
      V3_0,   // Version 3.0	Leichte Fehlerkorrekturen und Integration des Maschinensprachemonitors TIM, schnelle Garbage Collection. CBM 3000 Serie (Standard) und PET 2001 (angezeigte Versionsnummer dort ist allerdings noch 2).
      V4_0,   // Version 4.0	Neue Befehle für leichtere Handhabung für Diskettenlaufwerke für CBM 4000. Auch durch ROM-Austausch für CBM 3000 Serie und PET 2001 verfügbar.
      V4_1,   // Version 4.1	Fehlerkorrigierte Fassung der Version 4.0 mit erweiterter Bildschirmeditor für CBM 8000. Auch durch ROM-Austausch für CBM 3000/4000 Serie verfügbar.
      V4_2,   // Version 4.2	Geänderte und ergänzte Befehle für den CBM 8096.
      VIC_BASIC_V2,   //  Funktionsumfang von V 2.0 mit Korrekturen aus der Version-4-Linie. Einsatz im VC20.
      V4_PLUS,        // (intern bis 4.75)	Neue Befehle und RAM-Unterstützung bis 256 KByte für CBM-II-Serie (CBM 500, 6x0, 7x0). Fortsetzung und Ende der Version-4-Linie.
      C64_BASIC_V2,   //  Anpassung des VIC BASIC V2 für VC10, C64, C128 (C64-Modus), SX64, PET 64.
      V3_5,           // Version 3.5	Neue Befehle für die Heimcomputer C16/116 und Plus/4. Zusammenführung aus C64 BASIC V2 und Version-4-Linie.
      V3_6,           // Version 3.6	Neue Befehle für LCD-Prototypen.
      V7_0,           // Version 7.0	Neue Befehle für den C128/D/DCR. Weiterentwicklung des C16/116 BASIC 3.5 .
      V10_0           // Version 10 Neue Befehle für C65, beinhaltet sehr viele Fehler, kam aus dem Entwicklungsstadium nicht heraus. Weiterentwicklung des BASIC 7.0.
    }

    public class ParserSettings
    {
      public bool         StripSpaces = true;
      public BasicVersion Version = BasicVersion.C64_BASIC_V2;
    };

    public enum TokenValue
    {
      BLACK = 0,
      WHITE = 1,
      RED,
      CYAN,
      PURPLE,
      GREEN,
      BLUE,
      YELLOW,
      ORANGE,
      BROWN,
      LIGHT_RED,
      DARK_GREY,
      GREY,
      LIGHT_GREEN,
      LIGHT_BLUE,
      LIGHT_GREY,
      REVERSE_ON,
      REVERSE_OFF,
      CURSOR_DOWN,
      CURSOR_UP,
      CURSOR_LEFT,
      CURSOR_RIGHT,
      DELETE,
      INSERT,
      CLEAR,
      HOME,
      F1,
      F3,
      F5,
      F7,
      F2,
      F4,
      F6,
      F8,
      INDIRECT_KEY
    };

    public enum RenumberResult
    {
      OK = 0,
      TOO_MANY_LINES,
      NOTHING_TO_DO
    };

    public class ActionToken
    {
      public string Replacement = "";
      public int ByteValue = 0;
      public TokenValue TokenValue = TokenValue.BLACK;
    };

    internal class Token
    {
      public enum Type
      {
        LINE_NUMBER,
        BASIC_TOKEN,
        STRING_LITERAL,
        NUMERIC_LITERAL,
        DIRECT_TOKEN,
        EX_BASIC_TOKEN,
        MACRO,
        COMMENT,
        VARIABLE
      };
      public Type       TokenType = Type.DIRECT_TOKEN;
      public string     Content = "";
      public int        ByteValue = 0;
      public int        StartIndex = 0;
    };


    public class Opcode
    {
      public string         Command = "";
      public int            ByteValue = -1;
      public string         ShortCut = null;

      public Opcode( string Command, int ByteValue )
      {
        this.Command    = Command;
        this.ByteValue  = ByteValue;
      }

      public Opcode( string Command, int ByteValue, string ShortCut )
      {
        this.Command    = Command;
        this.ByteValue  = ByteValue;
        this.ShortCut   = ShortCut;
      }
    };

    internal class LineInfo
    {
      public int                      LineIndex = 0;
      public int                      LineNumber = 0;
      public GR.Memory.ByteBuffer     LineData = new GR.Memory.ByteBuffer();
      public GR.Collections.Set<int>  ReferencedLineNumbers = new GR.Collections.Set<int>();
      public string                   Line = "";
      public System.Collections.Generic.List<Token>   Tokens = new List<Token>();
    };

    public static Dictionary<string, Opcode> m_Opcodes = new Dictionary<string, Opcode>();
    public static SortedDictionary<byte, Opcode> m_OpcodesFromByte = new SortedDictionary<byte, Opcode>();
    public static Dictionary<string, Opcode> m_ExOpcodes = new Dictionary<string, Opcode>();

    private GR.Collections.Map<int, LineInfo> m_LineInfos = new GR.Collections.Map<int, LineInfo>();

    public static GR.Collections.Map<string, ActionToken>     ActionTokens = new GR.Collections.Map<string, ActionToken>();
    public static GR.Collections.Map<TokenValue, ActionToken> ActionTokenByValue = new GR.Collections.Map<TokenValue, ActionToken>();
    public static GR.Collections.Map<byte, ActionToken>       ActionTokenByByteValue = new GR.Collections.Map<byte,ActionToken>();
    public ParserSettings       Settings = new ParserSettings();

    public Types.ASM.FileInfo           ASMFileInfo = new C64Studio.Types.ASM.FileInfo();
    public Types.ASM.FileInfo           InitialFileInfo = null;


    static BasicFileParser()
    {

      // TODO - wieso??
      //  -> überschreibt echte Tokens!
      /*
      foreach ( ActionToken token in ActionTokens.Values )
      {
        AddOpcode( token.Replacement, token.ByteValue );
      }
       */
    }



    public BasicFileParser( ParserSettings Settings )
    {
      LabelMode = false;
      this.Settings = Settings;
      SetBasicVersion( Settings.Version );
    }



    public BasicFileParser( ParserSettings Settings, string Filename )
    {
      this.Settings = Settings;
      SetBasicVersion( Settings.Version );
      LabelMode   = false;
      m_Filename  = Filename;
    }



    private static void AddOpcode( string Opcode, int ByteValue )
    {
      var opcode = new Opcode( Opcode, ByteValue );
      m_Opcodes[Opcode] = opcode;
      m_OpcodesFromByte[(byte)ByteValue] = opcode;
    }



    private static void AddOpcode( string Opcode, int ByteValue, string ShortCut )
    {
      var opcode = new Opcode( Opcode, ByteValue, ShortCut );
      m_Opcodes[Opcode] = opcode;
      m_OpcodesFromByte[(byte)ByteValue] = opcode;
    }



    private static void AddExOpcode( string Opcode, int ByteValue )
    {
      m_ExOpcodes[Opcode] = new Opcode( Opcode, ByteValue );
    }



    public bool LabelMode
    {
      get;
      set;
    }



    private static void AddActionToken( TokenValue Value, string Token, byte ByteValue )
    {
      ActionToken token = new ActionToken();
      token.Replacement = Token;
      token.ByteValue = ByteValue;
      token.TokenValue = Value;

      ActionTokens[Token.ToUpper()] = token;
      ActionTokenByByteValue[ByteValue] = token;
      ActionTokenByValue[Value] = token;

      /*
      if ( PETSCII.ContainsKey( (char)ByteValue ) )
      {
        Debug.Log( "!!AddActionToken overrides entry for " + ByteValue );
      }
      PETSCII[(char)ByteValue] = ByteValue;*/
    }



    public void SetBasicVersion( BasicVersion Version )
    {
      ActionTokens.Clear();
      ActionTokenByByteValue.Clear();
      ActionTokenByValue.Clear();
      m_ExOpcodes.Clear();
      m_Opcodes.Clear();
      m_OpcodesFromByte.Clear();

      if ( ( Version == BasicVersion.V2_0 )
      ||   ( Version == BasicVersion.V3_5 )
      ||   ( Version == BasicVersion.V7_0 ) )
      {
        AddOpcode( "END", 0x80, "eN" );
        AddOpcode( "FOR", 0x81, "fO" );
        AddOpcode( "NEXT", 0x82, "nE" );
        AddOpcode( "DATA", 0x83, "dA" );
        AddOpcode( "INPUT#", 0x84, "iN" );
        AddOpcode( "INPUT", 0x85 );
        AddOpcode( "DIM", 0x86, "dI" );
        AddOpcode( "READ", 0x87, "rE" );
        AddOpcode( "LET", 0x88, "lE" );
        AddOpcode( "GOTO", 0x89, "gO" );
        AddOpcode( "RUN", 0x8A, "rU" );
        AddOpcode( "IF", 0x8B );
        AddOpcode( "RESTORE", 0x8C, "reS" );
        AddOpcode( "GOSUB", 0x8D, "goS" );
        AddOpcode( "RETURN", 0x8E, "reT" );
        AddOpcode( "REM", 0x8F );
        AddOpcode( "STOP", 0x90, "sT" );
        AddOpcode( "ON", 0x91 );
        AddOpcode( "WAIT", 0x92, "wA" );
        AddOpcode( "LOAD", 0x93, "lO" );
        AddOpcode( "SAVE", 0x94, "sA" );
        AddOpcode( "VERIFY", 0x95, "vE" );
        AddOpcode( "DEF", 0x96, "dE" );
        AddOpcode( "POKE", 0x97, "pO" );
        AddOpcode( "PRINT#", 0x98, "pR" );
        AddOpcode( "?", 0x99 );
        AddOpcode( "PRINT", 0x99 );
        AddOpcode( "CONT", 0x9A, "cO" );
        AddOpcode( "LIST", 0x9B, "lI" );
        AddOpcode( "CLR", 0x9C, "cL" );
        AddOpcode( "CMD", 0x9D, "cM" );
        AddOpcode( "SYS", 0x9E, "sY" );
        AddOpcode( "OPEN", 0x9F, "oP" );
        AddOpcode( "CLOSE", 0xA0, "clO" );
        AddOpcode( "GET", 0xA1, "gE" );
        AddOpcode( "NEW", 0xA2 );
        AddOpcode( "TAB(", 0xA3, "tA" );
        AddOpcode( "TO", 0xA4 );
        AddOpcode( "FN", 0xA5 );
        AddOpcode( "SPC(", 0xA6, "sP" );
        AddOpcode( "THEN", 0xA7, "tH" );
        AddOpcode( "NOT", 0xA8, "nO" );
        AddOpcode( "STEP", 0xA9, "stE" );
        AddOpcode( "+", 0xAA );
        AddOpcode( "-", 0xAB );
        AddOpcode( "*", 0xAC );
        AddOpcode( "/", 0xAD );
        //AddOpcode( "" + (char)0xee1e, 0xAE );
        AddOpcode( "" + (char)0x5e, 0xAE );
        AddOpcode( "AND", 0xAF, "aN" );
        AddOpcode( "OR", 0xB0 );
        AddOpcode( ">", 0xB1 );
        AddOpcode( "=", 0xB2 );
        AddOpcode( "<", 0xB3 );
        AddOpcode( "SGN", 0xB4, "sG" );
        AddOpcode( "INT", 0xB5 );
        AddOpcode( "ABS", 0xB6, "aB" );
        AddOpcode( "USR", 0xB7, "uS" );
        AddOpcode( "FRE", 0xB8, "fE" );
        AddOpcode( "POS", 0xB9 );
        AddOpcode( "SQR", 0xBA, "sQ" );
        AddOpcode( "RND", 0xBB, "rN" );
        AddOpcode( "LOG", 0xBC );
        AddOpcode( "EXP", 0xBD, "eX" );
        AddOpcode( "COS", 0xBE );
        AddOpcode( "SIN", 0xBF, "sI" );
        AddOpcode( "TAN", 0xC0 );
        AddOpcode( "ATN", 0xC1, "aT" );
        AddOpcode( "PEEK", 0xC2, "pE" );
        AddOpcode( "LEN", 0xC3 );
        AddOpcode( "STR$", 0xC4, "stR" );
        AddOpcode( "VAL", 0xC5, "vA" );
        AddOpcode( "ASC", 0xC6, "aS" );
        AddOpcode( "CHR$", 0xC7, "cH" );
        AddOpcode( "LEFT$", 0xC8, "leF" );
        AddOpcode( "RIGHT$", 0xC9, "rI" );
        AddOpcode( "MID$", 0xCA, "mI" );
        AddOpcode( "GO", 0xCB );

        // C64Studio extension
        AddExOpcode( "LABEL", 0xF0 );
      }

      if ( ( Version == BasicVersion.V3_5 )
      ||   ( Version == BasicVersion.V7_0 ) )
      {
        AddOpcode( "RGR", 0xcc );
        AddOpcode( "RCLR", 0xcd );
        AddOpcode( "RLUM", 0xce );
        AddOpcode( "JOY", 0xcf );
        AddOpcode( "RDOT", 0xd0 );
        AddOpcode( "DEC", 0xd1 );
        AddOpcode( "HEX$", 0xd2 );
        AddOpcode( "ERR$", 0xd3 );
        AddOpcode( "INSTR", 0xd4 );
        AddOpcode( "ELSE", 0xd5 );
        AddOpcode( "RESUME", 0xd6 );
        AddOpcode( "TRAP", 0xd7 );
        AddOpcode( "TRON", 0xd8 );
        AddOpcode( "TROFF", 0xd9 );
        AddOpcode( "SOUND", 0xda );
        AddOpcode( "VOL", 0xdb );
        AddOpcode( "AUTO", 0xdc );
        AddOpcode( "PUDEF", 0xdd );
        AddOpcode( "GRAPHIC", 0xde );
        AddOpcode( "PAINT", 0xdf );
        AddOpcode( "CHAR", 0xe0 );
        AddOpcode( "BOX", 0xe1 );
        AddOpcode( "CIRCLE", 0xe2 );
        AddOpcode( "GSHAPE", 0xe3 );
        AddOpcode( "SSHAPE", 0xe4 );
        AddOpcode( "DRAW", 0xe5 );
        AddOpcode( "LOCATE", 0xe6 );
        AddOpcode( "COLOR", 0xe7 );
        AddOpcode( "SCNCLR", 0xe8 );
        AddOpcode( "SCALE", 0xe9 );
        AddOpcode( "HELP", 0xea );
        AddOpcode( "DO", 0xeb );
        AddOpcode( "LOOP", 0xec );
        AddOpcode( "EXIT", 0xed );
        AddOpcode( "DIRECTORY", 0xee );
        AddOpcode( "DSAVE", 0xef );
        AddOpcode( "DLOAD", 0xf0 );
        AddOpcode( "HEADER", 0xf1 );
        AddOpcode( "SCRATCH", 0xf2 );
        AddOpcode( "COLLECT", 0xf3 );
        AddOpcode( "COPY", 0xf4 );
        AddOpcode( "RENAME", 0xf5 );
        AddOpcode( "BACKUP", 0xf6 );
        AddOpcode( "DELETE", 0xf7 );
        AddOpcode( "RENUMBER", 0xf8 );
        AddOpcode( "KEY", 0xf9 );
        AddOpcode( "MONITOR", 0xfa );
        AddOpcode( "USING", 0xfb );
        AddOpcode( "UNTIL", 0xfc );
        AddOpcode( "WHILE", 0xfd );
      }

      if ( Version == BasicVersion.V7_0 )
      {
        // override without or different short cuts
        AddOpcode( "END", 0x80 );
        AddOpcode( "FOR", 0x81 );
        AddOpcode( "NEXT", 0x82 );
        AddOpcode( "READ", 0x87, "reA" );
        AddOpcode( "STOP", 0x90, "stO" );
        AddOpcode( "POKE", 0x97, "poK" );
        AddOpcode( "CONT", 0x9A );
        AddOpcode( "SYS", 0x9E );
        AddOpcode( "SPC(", 0xA6 );
        AddOpcode( "PEEK", 0xC2, "peE" );

        m_Opcodes.Remove( "RLUM" );
        m_OpcodesFromByte.Remove( 0xce );


        // new commands
        AddOpcode( "POT", 0xce02 );
        AddOpcode( "BUMP", 0xce03 );
        AddOpcode( "PEN", 0xce04 );
        AddOpcode( "RSPPOS", 0xce05 );
        AddOpcode( "RSPRITE", 0xce06 );
        AddOpcode( "RSPCOLOR", 0xce07 );
        AddOpcode( "XOR", 0xce08 );
        AddOpcode( "RWINDOW", 0xce09 );
        AddOpcode( "POINTER", 0xce0a );

        AddOpcode( "BANK", 0xfe02 );
        AddOpcode( "FILTER", 0xfe03 );
        AddOpcode( "PLAY", 0xfe04 );
        AddOpcode( "TEMPO", 0xfe05 );
        AddOpcode( "MOVSPR", 0xfe06 );
        AddOpcode( "SPRITE", 0xfe07 );
        AddOpcode( "SPRCOLOR", 0xfe08 );
        AddOpcode( "RREG", 0xfe09 );
        AddOpcode( "ENVELOPE", 0xfe0a );
        AddOpcode( "SLEEP", 0xfe0b );
        AddOpcode( "CATALOG", 0xfe0c );
        AddOpcode( "DOPEN", 0xfe0d );
        AddOpcode( "APPEND", 0xfe0e );
        AddOpcode( "DCLOSE", 0xfe0f );
        AddOpcode( "BSAVE", 0xfe10 );
        AddOpcode( "BLOAD", 0xfe11 );
        AddOpcode( "RECORD", 0xfe12 );
        AddOpcode( "CONCAT", 0xfe13 );
        AddOpcode( "DVERIFY", 0xfe14 );
        AddOpcode( "DCLEAR", 0xfe15 );
        AddOpcode( "SPRSAV", 0xfe16 );
        AddOpcode( "COLLISION", 0xfe17 );
        AddOpcode( "BEGIN", 0xfe18 );
        AddOpcode( "BEND", 0xfe19 );
        AddOpcode( "WINDOW", 0xfe1a );
        AddOpcode( "BOOT", 0xfe1b );
        AddOpcode( "WIDTH", 0xfe1c );
        AddOpcode( "SPRDEF", 0xfe1d );
        AddOpcode( "QUIT", 0xfe1e );
        AddOpcode( "STASH", 0xfe1f );
        AddOpcode( "FETCH", 0xfe21 );
        AddOpcode( "SWAP", 0xfe23 );
        AddOpcode( "OFF", 0xfe24 );
        AddOpcode( "FAST", 0xfe25 );
        AddOpcode( "SLOW", 0xfe26 );
      }

      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-A}", 0xb0 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-B}", 0xbf );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-C}", 0xbc );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-D}", 0xac );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-E}", 0xb1 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-F}", 0xbb );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-G}", 0xa5 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-H}", 0xb4 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-I}", 0xa2 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-J}", 0xb5 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-K}", 0xa1 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-L}", 0xb6 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-M}", 0xaa );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-N}", 0xa7 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-O}", 0xb9 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-P}", 0xaf );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-Q}", 0xab );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-R}", 0xb2 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-S}", 0xae );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-T}", 0xa3 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-U}", 0xb8 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-V}", 0xbe );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-W}", 0xb3 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-X}", 0xbd );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-Z}", 0xad );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-Y}", 0xb7 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-+}", 0xa6 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-@}", 0xa4 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM--}", 0x7c );

      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-A}", 0x61 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-B}", 0x62 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-C}", 0x63 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-D}", 0x64 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-E}", 0x65 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-F}", 0x66 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-G}", 0x67 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-H}", 0x68 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-I}", 0x69 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-J}", 0x6a );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-K}", 0x6b );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-L}", 0x6c );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-M}", 0x6d );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-N}", 0x6e );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-O}", 0x6f );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-P}", 0x70 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-Q}", 0x71 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-R}", 0x72 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-S}", 0x73 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-T}", 0x74 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-U}", 0x75 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-V}", 0x76 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-W}", 0x77 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-X}", 0x78 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-Y}", 0x79 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-Z}", 0x7a );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-ARROWUP}", 0xff );   // PI
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-ARROWLEFT}", 0x5f );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-+}", 0x7b );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-*}", 0x60 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT--}", 0x7d );

      AddActionToken( TokenValue.INDIRECT_KEY, "{SHIFT-£}", 169 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-£}", 168 );
      AddActionToken( TokenValue.INDIRECT_KEY, "{CBM-*}", 127 );

      AddActionToken( TokenValue.BLACK, "{black}", 144 );
      AddActionToken( TokenValue.BLACK, "{blk}", 144 );
      AddActionToken( TokenValue.WHITE, "{white}", 5 );
      AddActionToken( TokenValue.WHITE, "{wht}", 5 );
      AddActionToken( TokenValue.RED, "{red}", 28 );
      AddActionToken( TokenValue.CYAN, "{cyn}", 159 );
      AddActionToken( TokenValue.PURPLE, "{purple}", 156 );
      AddActionToken( TokenValue.PURPLE, "{pur}", 156 );
      AddActionToken( TokenValue.GREEN, "{green}", 30 );
      AddActionToken( TokenValue.GREEN, "{grn}", 30 );
      AddActionToken( TokenValue.BLUE, "{blue}", 31 );
      AddActionToken( TokenValue.BLUE, "{blu}", 31 );
      AddActionToken( TokenValue.YELLOW, "{yellow}", 158 );
      AddActionToken( TokenValue.YELLOW, "{yel}", 158 );
      AddActionToken( TokenValue.ORANGE, "{orange}", 129 );
      AddActionToken( TokenValue.ORANGE, "{orng}", 129 );
      AddActionToken( TokenValue.BROWN, "{brown}", 149 );
      AddActionToken( TokenValue.BROWN, "{brn}", 149 );
      AddActionToken( TokenValue.LIGHT_RED, "{light red}", 150 );
      AddActionToken( TokenValue.LIGHT_RED, "{lred}", 150 );
      AddActionToken( TokenValue.DARK_GREY, "{dark gray}", 151 );
      AddActionToken( TokenValue.DARK_GREY, "{dark grey}", 151 );
      AddActionToken( TokenValue.DARK_GREY, "{gry1}", 151 );
      AddActionToken( TokenValue.GREY, "{gray}", 152 );
      AddActionToken( TokenValue.GREY, "{grey}", 152 );
      AddActionToken( TokenValue.GREY, "{gry2}", 152 );
      AddActionToken( TokenValue.LIGHT_GREEN, "{light green}", 153 );
      AddActionToken( TokenValue.LIGHT_GREEN, "{lgrn}", 153 );
      AddActionToken( TokenValue.LIGHT_BLUE, "{light blue}", 154 );
      AddActionToken( TokenValue.LIGHT_BLUE, "{lblu}", 154 );
      AddActionToken( TokenValue.LIGHT_GREY, "{light gray}", 155 );
      AddActionToken( TokenValue.LIGHT_GREY, "{light grey}", 155 );
      AddActionToken( TokenValue.LIGHT_GREY, "{gry3}", 155 );
      AddActionToken( TokenValue.REVERSE_ON, "{reverse on}", 18 );
      AddActionToken( TokenValue.REVERSE_ON, "{rvson}", 18 );
      AddActionToken( TokenValue.REVERSE_ON, "{rvon}", 18 );
      AddActionToken( TokenValue.REVERSE_OFF, "{reverse off}", 146 );
      AddActionToken( TokenValue.REVERSE_OFF, "{rvsoff}", 146 );
      AddActionToken( TokenValue.REVERSE_OFF, "{rvof}", 146 );
      AddActionToken( TokenValue.CURSOR_DOWN, "{cursor down}", 17 );
      AddActionToken( TokenValue.CURSOR_DOWN, "{down}", 17 );
      AddActionToken( TokenValue.CURSOR_UP, "{cursor up}", 145 );
      AddActionToken( TokenValue.CURSOR_UP, "{up}", 145 );
      AddActionToken( TokenValue.CURSOR_LEFT, "{cursor left}", 157 );
      AddActionToken( TokenValue.CURSOR_LEFT, "{left}", 157 );
      AddActionToken( TokenValue.CURSOR_RIGHT, "{cursor right}", 29 );
      AddActionToken( TokenValue.CURSOR_RIGHT, "{rght}", 29 );
      AddActionToken( TokenValue.CURSOR_RIGHT, "{right}", 29 );
      AddActionToken( TokenValue.DELETE, "{del}", 20 );
      AddActionToken( TokenValue.INSERT, "{insert}", 148 );
      AddActionToken( TokenValue.INSERT, "{ins}", 148 );
      AddActionToken( TokenValue.CLEAR, "{clr}", 147 );
      AddActionToken( TokenValue.HOME, "{home}", 19 );
      AddActionToken( TokenValue.F1, "{F1}", 133 );
      AddActionToken( TokenValue.F3, "{F3}", 134 );
      AddActionToken( TokenValue.F5, "{F5}", 135 );
      AddActionToken( TokenValue.F7, "{F7}", 136 );
      AddActionToken( TokenValue.F2, "{F2}", 137 );
      AddActionToken( TokenValue.F4, "{F4}", 138 );
      AddActionToken( TokenValue.F6, "{F6}", 139 );
      AddActionToken( TokenValue.F8, "{F8}", 140 );
    }



    private void CleanLines( string[] Lines )
    {
      for ( int i = 0; i < Lines.Length; ++i )
      {
        string tempLine = Lines[i].Trim();
        tempLine = tempLine.Replace( '\t', ' ' );
        Lines[i] = tempLine;
      }
    }



    public override void Clear()
    {
      m_CompileTarget     = Types.CompileTargetType.PRG;
      m_CompileTargetFile = null;
      //m_CompileCurrentAddress = -1;

      AssembledOutput = null;
      Messages.Clear();
      m_LineInfos.Clear();
      m_ErrorMessages = 0;
      m_WarningMessages = 0;
      m_Filename = "";
    }



    public override GR.Collections.MultiMap<string, Types.SymbolInfo> KnownTokenInfo()
    {
      GR.Collections.MultiMap<string, Types.SymbolInfo> knownTokens = new GR.Collections.MultiMap<string, Types.SymbolInfo>();

      /*
      knownTokens.AddRange( m_Labels.Keys );
      knownTokens.AddRange( m_UnparsedLabels.Keys );
       */

      return knownTokens;
    }



    private void DumpSourceInfos()
    {
      /*
      // source infos
      foreach ( KeyValuePair<int,SourceInfo> pair in m_SourceInfo )
      {
        Debug.Log( "From line " + pair.Value.GlobalStartLine + " to " + ( pair.Value.GlobalStartLine + pair.Value.LineCount ) + ", local start " + pair.Value.LocalStartLine + " : " + pair.Value.Filename );
      }*/
    }



    public override bool Parse( string Content, ProjectConfig Configuration, CompileConfig Config )
    {
      string[] lines = Content.Split( '\n' );

      CleanLines( lines );

      ASMFileInfo.Labels.Clear();
      IncludePreviousSymbols();

      ProcessLines( lines, LabelMode );

      DumpSourceInfos();

      if ( m_ErrorMessages > 0 )
      {
        return false;
      }

      return true;
    }



    private void IncludePreviousSymbols()
    {
      // include previous symbols
      if ( InitialFileInfo != null )
      {
        foreach ( var entry in InitialFileInfo.Labels )
        {
          if ( ( entry.Value.Type != C64Studio.Types.SymbolInfo.Types.PREPROCESSOR_CONSTANT_1 )
          &&   ( entry.Value.Type != C64Studio.Types.SymbolInfo.Types.PREPROCESSOR_CONSTANT_2 )
          &&   ( entry.Value.Type != C64Studio.Types.SymbolInfo.Types.PREPROCESSOR_LABEL ) )
          {
            if ( ( entry.Value.Type == C64Studio.Types.SymbolInfo.Types.LABEL )
            &&   ( entry.Key.StartsWith( ASMFileParser.InternalLabelPrefix ) ) )
            {
              // do not pass on internal local labels
              continue;
            }
            C64Studio.Types.SymbolInfo    symbol = new C64Studio.Types.SymbolInfo();
            symbol.AddressOrValue = entry.Value.AddressOrValue;
            symbol.DocumentFilename = entry.Value.DocumentFilename;
            symbol.LocalLineIndex = entry.Value.LocalLineIndex;
            symbol.Name = entry.Value.Name;
            symbol.Type = entry.Value.Type;
            symbol.Used = true;
            symbol.Zone = entry.Value.Zone;
            symbol.FromDependency = true;
            symbol.Info = entry.Value.Info;

            ASMFileInfo.Labels.Add( entry.Key, symbol );
          }
        }
      }
    }



    private void ParseToken( string CurToken, LineInfo Info, ref bool lastOpcodeWasReferencingLineNumber )
    {
      string token2 = CurToken.Trim().ToUpper();
      if ( m_Opcodes.ContainsKey( token2 ) )
      {
        Opcode opCode = m_Opcodes[token2];

        Info.LineData.AppendU8( (byte)opCode.ByteValue );

        Token opcodeToken = new Token();
        opcodeToken.TokenType = Token.Type.BASIC_TOKEN;
        opcodeToken.ByteValue = (byte)opCode.ByteValue;
        opcodeToken.Content = opCode.Command;
        Info.Tokens.Add( opcodeToken );
      }
      else if ( m_ExOpcodes.ContainsKey( token2 ) )
      {
        Opcode opCode = m_ExOpcodes[token2];

        Info.LineData.AppendU8( (byte)opCode.ByteValue );

        Token opcodeToken = new Token();
        opcodeToken.TokenType = Token.Type.EX_BASIC_TOKEN;
        opcodeToken.ByteValue = (byte)opCode.ByteValue;
        opcodeToken.Content = opCode.Command;
        Info.Tokens.Add( opcodeToken );
      }
      else if ( token2.Length > 0 )
      {
        if ( lastOpcodeWasReferencingLineNumber )
        {
          int dummy = -1;
          if ( int.TryParse( token2, out dummy ) )
          {
            Info.ReferencedLineNumbers.Add( dummy );
          }
          lastOpcodeWasReferencingLineNumber = false;
        }
        //Debug.Log( "Token:" + token2 );
        Token literalToken = new Token();
        literalToken.TokenType = Token.Type.STRING_LITERAL;
        literalToken.Content = token2;
        Info.Tokens.Add( literalToken );
        for ( int i = 0; i < token2.Length; ++i )
        {
          Info.LineData.AppendU8( (byte)token2[i] );
        }
      }
    }



    private string ReplaceMacros( int LineIndex, string Line )
    {
      int bracketStartPos = Line.IndexOf( '{' );
      int bracketEndPos = -1;

      if ( bracketStartPos == -1 )
      {
        return Line;
      }
      int     lastStartPos = 0;
      string  result = "";

      while ( bracketStartPos != -1 )
      {
        if ( bracketStartPos > lastStartPos + 1 )
        {
          result += Line.Substring( lastStartPos, bracketStartPos - lastStartPos );
        }

        bracketEndPos = Line.IndexOf( '}', bracketStartPos );
        if ( bracketEndPos == -1 )
        {
          AddError( LineIndex, Types.ErrorCode.E1005_MISSING_CLOSING_BRACKET, "Missing closing bracket on macro" );
          return result;
        }
        string  macro = Line.Substring( bracketStartPos, bracketEndPos - bracketStartPos + 1 );
        int     macroCount = 1;

        macro = DetermineMacroCount( macro, out macroCount );


        bool  foundMacro = false;
        foreach ( var key in Types.ConstantData.AllPhysicalKeyInfos )
        {
          if ( key.Replacements.Contains( macro ) )
          {
            for ( int i = 0; i < macroCount; ++i )
            {
              result += (char)key.PetSCIIValue;
            }
            foundMacro = true;
            break;
          }
        }
        if ( !foundMacro )
        {
          byte  petsciiValue = 0;
          if ( byte.TryParse( macro, out petsciiValue ) )
          {
            for ( int i = 0; i < macroCount; ++i )
            {
              result += (char)petsciiValue;
            }
          }
          else
          {
            AddError( LineIndex, Types.ErrorCode.E1301_MACRO_UNKNOWN, "Unknown macro " + macro );
          }
        }
        lastStartPos = bracketEndPos + 1;
        bracketStartPos = Line.IndexOf( '{', bracketEndPos );
      }
      if ( lastStartPos < Line.Length )
      {
        result += Line.Substring( lastStartPos );
      }
      return result;
    }



    /// <summary>
    /// looks for numbers at beginning of string, returns as separate entities (e.g. {4down} = "down" and 4}
    /// </summary>
    /// <param name="Macro"></param>
    /// <param name="MacroCount"></param>
    /// <returns></returns>
    public static string DetermineMacroCount( string Macro, out int MacroCount )
    {
      MacroCount = 1;
      if ( string.IsNullOrEmpty( Macro ) )
      {
        return Macro;
      }

      if ( !char.IsDigit( Macro[0] ) )
      {
        return Macro;
      }

      int     lastDigitPos = 0;

      while ( ( lastDigitPos + 1 < Macro.Length )
      &&      ( char.IsDigit( Macro[lastDigitPos + 1] ) ) )
      {
        ++lastDigitPos;
      }

      int.TryParse( Macro.Substring( 0, lastDigitPos + 1 ), out MacroCount );

      if ( ( MacroCount < 0 )
      ||   ( MacroCount > 999 ) )
      {
        MacroCount = 1;
      }

      return Macro.Substring( lastDigitPos + 1 ).TrimStart();
    }



    private void AddDirectToken( LineInfo Info, byte TokenValue, int StartIndex )
    {
      if ( ( TokenValue >= 0x30 )
      &&   ( TokenValue <= 0x39 ) )
      {
        // numeric, maybe attach with token before?
        if ( ( Info.Tokens.Count > 0 )
        &&   ( Info.Tokens[Info.Tokens.Count - 1].TokenType == Token.Type.NUMERIC_LITERAL )
        &&   ( Info.Tokens[Info.Tokens.Count - 1].ByteValue >= 0x30 )
        &&   ( Info.Tokens[Info.Tokens.Count - 1].ByteValue <= 0x39 ) )
        {
          // attach to previous token
          Info.Tokens[Info.Tokens.Count - 1].Content += Types.ConstantData.PetSCIIToChar[TokenValue].CharValue;
          Info.Tokens[Info.Tokens.Count - 1].TokenType = Token.Type.NUMERIC_LITERAL;

          Info.LineData.AppendU8( TokenValue );
          return;
        }
        Token numericToken = new Token();
        numericToken.TokenType = Token.Type.NUMERIC_LITERAL;
        numericToken.ByteValue = TokenValue;
        numericToken.Content = "" + Types.ConstantData.PetSCIIToChar[TokenValue].CharValue;
        numericToken.StartIndex = StartIndex;
        Info.Tokens.Add( numericToken );

        Info.LineData.AppendU8( TokenValue );
        return;
      }
      Token basicToken = new Token();
      basicToken.TokenType = Token.Type.DIRECT_TOKEN;
      basicToken.ByteValue = TokenValue;
      basicToken.Content = "" + Types.ConstantData.PetSCIIToChar[TokenValue].CharValue;
      basicToken.StartIndex = StartIndex;
      Info.Tokens.Add( basicToken );

      Info.LineData.AppendU8( TokenValue );
    }



    internal LineInfo PureTokenizeLine( string Line, int LineIndex )
    {
      //line = ReplaceMacros( lineIndex, line );
      int     endOfDigitPos = -1;
      for ( int i = 0; i < Line.Length; ++i )
      {
        if ( ( Line[i] < '0' )
        ||   ( Line[i] > '9' ) )
        {
          break;
        }
        ++endOfDigitPos;
      }
      LineInfo info = new LineInfo();
      info.LineIndex = LineIndex;
      if ( !LabelMode )
      {
        if ( endOfDigitPos == -1 )
        {
          AddError( LineIndex, Types.ErrorCode.E3000_BASIC_MISSING_LINE_NUMBER, "Missing line number" );
        }
        else
        {
          Token lineNumberToken = new Token();
          lineNumberToken.TokenType   = Token.Type.LINE_NUMBER;
          lineNumberToken.ByteValue   = GR.Convert.ToI32( Line.Substring( 0, endOfDigitPos + 1 ) );
          lineNumberToken.Content     = Line.Substring( 0, endOfDigitPos + 1 ).Trim();
          lineNumberToken.StartIndex  = 0;
          info.Tokens.Add( lineNumberToken );
          info.LineNumber = lineNumberToken.ByteValue;
          info.Line       = Line.Substring( endOfDigitPos + 1 ).Trim();
        }
      }

      int       posInLine = endOfDigitPos + 1;
      bool      insideMacro = false;
      bool      insideDataStatement = false;
      bool      insideStringLiteral = false;
      int       lastCharStartPos = -1;
      int       stringLiteralStartPos = -1;
      int       macroStartPos = -1;
      int       tempDataStartPos = -1;
      GR.Memory.ByteBuffer tempData = new GR.Memory.ByteBuffer();

      // translate line to actual PETSCII codes
      while ( posInLine < Line.Length )
      {
        char    curChar = Line[posInLine];

        if ( insideMacro )
        {
          if ( curChar == '}' )
          {
            insideMacro = false;

            if ( !insideStringLiteral )
            {
              string macro = Line.Substring( macroStartPos + 1, posInLine - macroStartPos - 1 ).ToUpper();

              Token tokenMacro = new Token();
              tokenMacro.TokenType = Token.Type.MACRO;
              tokenMacro.Content = macro;
              tokenMacro.StartIndex = macroStartPos;
              info.Tokens.Add( tokenMacro );
            }
          }
          ++posInLine;
          continue;
        }
        if ( curChar == '{' )
        {
          if ( tempData.Length > 0 )
          {
            ConsolidateTokens( info, tempData, tempDataStartPos );
            tempData.Clear();
            tempDataStartPos = -1;
          }
          insideMacro = true;
          macroStartPos = posInLine;
          ++posInLine;
          continue;
        }
        if ( curChar == '"' )
        {
          insideStringLiteral = !insideStringLiteral;
          if ( !insideStringLiteral )
          {
            Token tokenStringLiteral      = new Token();
            tokenStringLiteral.TokenType  = Token.Type.STRING_LITERAL;
            tokenStringLiteral.Content    = Line.Substring( stringLiteralStartPos, posInLine - stringLiteralStartPos + 1 );
            tokenStringLiteral.StartIndex = stringLiteralStartPos;
            info.Tokens.Add( tokenStringLiteral );
            ++posInLine;
            continue;
          }
          else
          {
            if ( tempData.Length > 0 )
            {
              ConsolidateTokens( info, tempData, tempDataStartPos );
              tempData.Clear();
              tempDataStartPos = -1;
            }
            stringLiteralStartPos = posInLine;
          }
        }
        if ( !insideStringLiteral )
        {
          if ( ( curChar >= 'A' )
          &&   ( curChar <= 'Z' ) )
          {
            if ( lastCharStartPos == -1 )
            {
              // start of chars, potential BASIC code
              if ( tempData.Length > 0 )
              {
                ConsolidateTokens( info, tempData, tempDataStartPos );
                tempData.Clear();
                tempDataStartPos = -1;
              }
              lastCharStartPos = posInLine;
            }
          }
          else
          {
            lastCharStartPos = -1;
          }

          if ( !Types.ConstantData.CharToC64Char.ContainsKey( curChar ) )
          {
            if ( !Types.ConstantData.CharToC64Char.ContainsKey( char.ToUpper( curChar ) ) )
            {
              AddError( LineIndex, Types.ErrorCode.E3002_BASIC_UNSUPPORTED_CHARACTER, "Unsupported character " + (int)curChar + " encountered" );
            }
            else if ( !Types.ConstantData.CharToC64Char[char.ToUpper( curChar )].HasPetSCII )
            {
              AddError( LineIndex, Types.ErrorCode.E3002_BASIC_UNSUPPORTED_CHARACTER, "Unsupported character " + (int)curChar + " encountered" );
            }
            else
            {
              if ( tempDataStartPos == -1 )
              {
                tempDataStartPos = posInLine;
              }
              tempData.AppendU8( Types.ConstantData.CharToC64Char[char.ToUpper( curChar )].PetSCIIValue );
            }
          }
          else if ( !Types.ConstantData.CharToC64Char[curChar].HasPetSCII )
          {
            AddError( LineIndex, Types.ErrorCode.E3002_BASIC_UNSUPPORTED_CHARACTER, "Unsupported character " + (int)curChar + " encountered" );
          }
          else
          {
            //if ( posInLine > endOfDigitPos + 1 )
            {
              if ( tempDataStartPos == -1 )
              {
                tempDataStartPos = posInLine;
              }
              tempData.AppendU8( Types.ConstantData.CharToC64Char[curChar].PetSCIIValue );
            }
          }
          // is there a BASIC token in there?
          if ( tempData.Length > 0 )
          {
            byte    nextByte = tempData.ByteAt( 0 );

            if ( nextByte == 32 )
            {
              // Space
              if ( tempData.Length > 0 )
              {
                ConsolidateTokens( info, tempData, tempDataStartPos );
                tempData.Clear();
                tempDataStartPos = -1;
              }
              ++posInLine;
              continue;
            }
            if ( nextByte == 0x3f )
            {
              // ? durch PRINT ersetzen
              /*
              if ( tempData.Length > 0 )
              {
                ConsolidateTokens( info, tempData, tempDataStartPos );
                tempData.Clear();
                tempDataStartPos = -1;
              }*/
              nextByte = 0x99;

              Token basicToken2      = new Token();
              basicToken2.TokenType  = Token.Type.BASIC_TOKEN;
              basicToken2.ByteValue  = nextByte;
              basicToken2.Content    = "" + Types.ConstantData.PetSCIIToChar[0x3f].CharValue;
              basicToken2.StartIndex = posInLine;
              info.Tokens.Add( basicToken2 );

              tempData.Clear();
              tempDataStartPos = -1;
              ++posInLine;
              continue;
            }
            // is there a token now?
            bool entryFound = true;
            bool potentialToken = false;
            foreach ( KeyValuePair<byte, Opcode> opcodeEntry in m_OpcodesFromByte )
            {
              Opcode  opcode = opcodeEntry.Value;

              entryFound = true;
              for ( int i = 0; i < opcode.Command.Length; ++i )
              {
                if ( i >= tempData.Length )
                {
                  // can't be this token, length not complete
                  entryFound = false;
                  if ( i > 0 )
                  {
                    potentialToken = true;
                  }
                  break;
                }
                if ( tempData.ByteAt( i ) != (byte)opcode.Command[i] )
                {
                  entryFound = false;
                  break;
                }
              }
              if ( entryFound )
              {
                Token basicToken4 = new Token();
                basicToken4.TokenType  = Token.Type.BASIC_TOKEN;
                basicToken4.ByteValue  = (byte)opcode.ByteValue;
                basicToken4.Content    = opcode.Command;
                basicToken4.StartIndex = tempDataStartPos;
                info.Tokens.Add( basicToken4 );

                tempData.Clear();
                tempDataStartPos = -1;
                insideDataStatement = ( opcode.Command == "DATA" );
                if ( opcode.Command == "REM" )
                {
                  if ( basicToken4.StartIndex + 3 < Line.Length )
                  {
                    // everything else is comment
                    Token comment       = new Token();
                    comment.TokenType   = Token.Type.COMMENT;
                    comment.Content     = Line.Substring( basicToken4.StartIndex + 3 );
                    comment.StartIndex  = basicToken4.StartIndex + 3;
                    info.Tokens.Add( comment );

                    return info;
                  }
                }
                break;
              }
            }
            if ( entryFound )
            {
              ++posInLine;
              continue;
            }
            // is it an extended token?
            entryFound = true;
            foreach ( KeyValuePair<string, Opcode> opcodeEntry in m_ExOpcodes )
            {
              Opcode opcode = opcodeEntry.Value;

              entryFound = true;
              for ( int i = 0; i < opcode.Command.Length; ++i )
              {
                if ( i >= tempData.Length )
                {
                  // can't be this token
                  if ( i > 0 )
                  {
                    potentialToken = true;
                  }
                  entryFound = false;
                  break;
                }
                if ( tempData.ByteAt( i ) != (byte)opcode.Command[i] )
                {
                  entryFound = false;
                  break;
                }
              }
              if ( entryFound )
              {
                Token basicToken5 = new Token();
                basicToken5.TokenType  = Token.Type.EX_BASIC_TOKEN;
                basicToken5.ByteValue  = (byte)opcode.ByteValue;
                basicToken5.Content    = opcode.Command;
                basicToken5.StartIndex = tempDataStartPos;
                info.Tokens.Add( basicToken5 );

                tempData.Clear();
                tempDataStartPos = -1;

                insideDataStatement = false;
                break;
              }
            }
            if ( entryFound )
            {
              ++posInLine;
              continue;
            }
            // not a token, add directly, but only if not a potential BASIC token!!
            if ( !potentialToken )
            {
              /*
              ConsolidateTokens( info, tempData, tempDataStartPos );
              tempData.Clear();
              tempDataStartPos = -1;*/
            }
          }
        }

        ++posInLine;
      }

      if ( ( tempDataStartPos != -1 )
      &&   ( tempDataStartPos < Line.Length ) )
      {
        // all the rest is one token
        ConsolidateTokens( info, tempData, tempDataStartPos );
      }

      return info;
    }



    /// <summary>
    /// combines variable names and numeric literals from single chars
    /// </summary>
    /// <param name="info"></param>
    /// <param name="tempData"></param>
    /// <param name="tempDataStartPos"></param>
    private void ConsolidateTokens( LineInfo info, ByteBuffer tempData, int TempDataStartPos )
    {
      bool      isVariableName = false;
      bool      isNumeric = false;
      int       lastTokenStartPos = 0;
      for ( int i = 0; i < tempData.Length; ++i )
      {
        byte    nextByte = tempData.ByteAt( i );

        bool    isCharNumeric = ( ( nextByte >= '0' )
        &&                        ( nextByte <= '9' ) );
        bool    isCharAlpha = ( ( nextByte >= 'A' )
        &&                      ( nextByte <= 'Z' ) );


        if ( isVariableName )
        {
          // we already started with a variable
          if ( ( !isCharNumeric )
          &&   ( !isCharAlpha )
          &&   ( nextByte != '$' )
          &&   ( nextByte != '%' ) )
          {
            // variable name is done
            isVariableName = false;
            Token basicToken6     = new Token();
            basicToken6.TokenType = Token.Type.VARIABLE;
            for ( int j = lastTokenStartPos; j < i; ++j )
            {
              basicToken6.Content += "" + Types.ConstantData.PetSCIIToChar[tempData.ByteAt( j )].CharValue;
            }
            basicToken6.StartIndex = TempDataStartPos + lastTokenStartPos;
            info.Tokens.Add( basicToken6 );

            lastTokenStartPos = i + 1;
          }
          else
          {
            continue;
          }
        }
        else if ( isNumeric )
        {
          if ( ( !isCharNumeric )
          &&   ( nextByte != '.' ) )
          {
            // numeric is done
            isNumeric = false;
            Token basicToken1       = new Token();
            basicToken1.TokenType = Token.Type.NUMERIC_LITERAL;
            for ( int j = lastTokenStartPos; j < i; ++j )
            {
              basicToken1.Content += "" + Types.ConstantData.PetSCIIToChar[tempData.ByteAt( j )].CharValue;
            }
            basicToken1.StartIndex = TempDataStartPos + lastTokenStartPos;
            info.Tokens.Add( basicToken1 );

            lastTokenStartPos = i + 1;
          }
          else
          {
            continue;
          }
        }
        // variable begins?
        if ( isCharAlpha )
        {
          isVariableName = true;
        }
        else if ( isCharNumeric )
        {
          isNumeric = true;
        }
        else
        {
          Token basicToken2       = new Token();
          basicToken2.TokenType   = Token.Type.DIRECT_TOKEN;
          basicToken2.ByteValue   = nextByte;
          basicToken2.Content     = "" + Types.ConstantData.PetSCIIToChar[nextByte].CharValue;
          basicToken2.StartIndex  = TempDataStartPos + i;
          info.Tokens.Add( basicToken2 );
          lastTokenStartPos = i + 1;
        }
      }
      if ( isVariableName )
      {
        Token basicToken3     = new Token();
        basicToken3.TokenType = Token.Type.VARIABLE;
        for ( int j = lastTokenStartPos; j < tempData.Length; ++j )
        {
          basicToken3.Content += "" + Types.ConstantData.PetSCIIToChar[tempData.ByteAt( j )].CharValue;
        }
        basicToken3.StartIndex = TempDataStartPos + lastTokenStartPos;
        info.Tokens.Add( basicToken3 );
      }
      else if ( isNumeric )
      {
        Token basicToken       = new Token();
        basicToken.TokenType = Token.Type.NUMERIC_LITERAL;
        for ( int j = lastTokenStartPos; j < tempData.Length; ++j )
        {
          basicToken.Content += "" + Types.ConstantData.PetSCIIToChar[tempData.ByteAt( j )].CharValue;
        }
        basicToken.StartIndex = TempDataStartPos + lastTokenStartPos;
        info.Tokens.Add( basicToken );
      }
      else
      {
        for ( int i = lastTokenStartPos; i < tempData.Length; ++i )
        {
          Token basicToken2       = new Token();
          basicToken2.TokenType   = Token.Type.DIRECT_TOKEN;
          basicToken2.Content     = "" + Types.ConstantData.PetSCIIToChar[tempData.ByteAt( i )].CharValue;
          basicToken2.StartIndex  = TempDataStartPos + i;
          info.Tokens.Add( basicToken2 );
          lastTokenStartPos = i + 1;
        }
      }
    }



    internal LineInfo TokenizeLine( string Line, int LineIndex, ref int LastLineNumber )
    {
      //line = ReplaceMacros( lineIndex, line );
      int     endOfDigitPos = -1;
      for ( int i = 0; i < Line.Length; ++i )
      {
        if ( ( Line[i] < '0' )
        ||   ( Line[i] > '9' ) )
        {
          break;
        }
        ++endOfDigitPos;
      }
      LineInfo info = new LineInfo();
      info.LineIndex  = LineIndex;
      info.Line       = Line;
      if ( !LabelMode )
      {
        if ( endOfDigitPos == -1 )
        {
          AddError( LineIndex, Types.ErrorCode.E3000_BASIC_MISSING_LINE_NUMBER, "Missing line number" );
        }
        else
        {
          Token lineNumberToken = new Token();
          lineNumberToken.TokenType = Token.Type.LINE_NUMBER;
          lineNumberToken.ByteValue = GR.Convert.ToI32( Line.Substring( 0, endOfDigitPos + 1 ) );
          lineNumberToken.Content = Line.Substring( 0, endOfDigitPos + 1 ).Trim();
          lineNumberToken.StartIndex = 0;
          info.Tokens.Add( lineNumberToken );
          info.LineNumber = lineNumberToken.ByteValue;
          if ( ( info.LineNumber < 0 )
          ||   ( info.LineNumber > 63999 ) )
          {
            AddError( LineIndex, Types.ErrorCode.E3001_BASIC_INVALID_LINE_NUMBER, "Unsupported line number, must be in the range 0 to 63999" );
          }
          else if ( info.LineNumber <= LastLineNumber )
          {
            AddError( LineIndex, Types.ErrorCode.E3001_BASIC_INVALID_LINE_NUMBER, "Line number not increasing, must be higher than the previous line number" );
          }
          LastLineNumber = info.LineNumber;
        }
      }

      int       posInLine = endOfDigitPos + 1;
      bool      insideMacro = false;
      bool      insideDataStatement = false;
      bool      insideREMStatement = false;
      int       macroStartPos = -1;
      GR.Memory.ByteBuffer tempData = new GR.Memory.ByteBuffer();

      // translate line to actual PETSCII codes
      while ( posInLine < Line.Length )
      {
        char    curChar = Line[posInLine];

        if ( insideMacro )
        {
          if ( curChar == '}' )
          {
            insideMacro = false;

            string macro = Line.Substring( macroStartPos + 1, posInLine - macroStartPos - 1 ).ToUpper();
            int macroCount = 1;

            macro = DetermineMacroCount( macro, out macroCount );

            bool  foundMacro = false;
            foreach ( var key in Types.ConstantData.AllPhysicalKeyInfos )
            {
              if ( key.Replacements.Contains( macro ) )
              {
                for ( int i = 0; i < macroCount; ++i )
                {
                  tempData.AppendU8( key.PetSCIIValue );
                }
                foundMacro = true;
                break;
              }
            }
            if ( !foundMacro )
            {
              byte  petsciiValue = 0;
              if ( byte.TryParse( macro, out petsciiValue ) )
              {
                for ( int i = 0; i < macroCount; ++i )
                {
                  tempData.AppendU8( petsciiValue );
                }
              }
              else if ( macro.StartsWith( "$" ) )
              {
                // a hex value?
                uint    hexValue = 0;

                if ( uint.TryParse( macro.Substring( 1 ), System.Globalization.NumberStyles.HexNumber, null, out hexValue ) )
                {
                  string    value = hexValue.ToString();

                  for ( int j = 0; j < macroCount; ++j )
                  {
                    for ( int i = 0; i < value.Length; ++i )
                    {
                      tempData.AppendU8( (byte)value[i] );
                    }
                  }
                }
                else
                {
                  AddError( LineIndex, Types.ErrorCode.E1301_MACRO_UNKNOWN, "Unknown macro " + macro );
                }
              }
              else
              {
                // do we have a label?
                if ( ASMFileInfo.Labels.ContainsKey( macro ) )
                {
                  string value = ASMFileInfo.Labels[macro].AddressOrValue.ToString();

                  for ( int j = 0; j < macroCount; ++j )
                  {
                    for ( int i = 0; i < value.Length; ++i )
                    {
                      tempData.AppendU8( (byte)value[i] );
                    }
                  }
                }
                else
                {
                  AddError( LineIndex, Types.ErrorCode.E1301_MACRO_UNKNOWN, "Unknown macro " + macro );
                }
              }
            }
          }
          ++posInLine;
          continue;
        }
        if ( curChar == '{' )
        {
          insideMacro = true;
          macroStartPos = posInLine;
          ++posInLine;
          continue;
        }
        // find actual byte value of char
        if ( !Types.ConstantData.CharToC64Char.ContainsKey( curChar ) )
        {
          if ( !Types.ConstantData.CharToC64Char.ContainsKey( char.ToUpper( curChar ) ) )
          {
            AddError( LineIndex, Types.ErrorCode.E3002_BASIC_UNSUPPORTED_CHARACTER, "Unsupported character " + (int)curChar + " encountered" );
          }
          else if ( !Types.ConstantData.CharToC64Char[char.ToUpper( curChar )].HasPetSCII )
          {
            AddError( LineIndex, Types.ErrorCode.E3002_BASIC_UNSUPPORTED_CHARACTER, "Unsupported character " + (int)curChar + " encountered" );
          }
          else
          {
            tempData.AppendU8( Types.ConstantData.CharToC64Char[char.ToUpper( curChar )].PetSCIIValue );
          }
        }
        else if ( !Types.ConstantData.CharToC64Char[curChar].HasPetSCII )
        {
          AddError( LineIndex, Types.ErrorCode.E3002_BASIC_UNSUPPORTED_CHARACTER, "Unsupported character " + (int)curChar + " encountered" );
        }
        else
        {
          if ( ( curChar != 32 )
          ||   ( posInLine > endOfDigitPos + 1 ) )
          {
            // strip spaces after line numbers
            tempData.AppendU8( Types.ConstantData.CharToC64Char[curChar].PetSCIIValue );
          }
        }

        ++posInLine;
      }
      //Debug.Log( "translated line to " + tempData.ToString() );

      // now the real token crunching
      int bytePos = 0;

      while ( bytePos < tempData.Length )
      {
        byte    nextByte = tempData.ByteAt( bytePos );
        if ( insideREMStatement )
        {
          AddDirectToken( info, nextByte, bytePos );
          ++bytePos;
          continue;
        }
        if ( nextByte < 128 )
        {
          // kein Token
          if ( nextByte == 32 )
          {
            // Space, direkt einfügen
            if ( !Settings.StripSpaces )
            {
              AddDirectToken( info, nextByte, bytePos );
            }
            ++bytePos;
            continue;
          }
          if ( nextByte == 34 )
          {
            // Anführungszeichen, abschliessendes Anführungszeichen suchen
            string stringLiteral = "";
            int     startIndex = bytePos;
            do
            {
              var c64Key = Types.ConstantData.FindC64KeyByPETSCII( nextByte );
              if ( ( c64Key != null )
              &&   ( nextByte != 32 )   // do not replace for Space
              &&   ( c64Key.Replacements.Count > 0 ) )
              {
                stringLiteral += "{" + c64Key.Replacements[0] + "}";
              }
              else
              {
                stringLiteral += Types.ConstantData.PetSCIIToChar[nextByte].CharValue;
              }
              info.LineData.AppendU8( nextByte );
              ++bytePos;
              if ( bytePos < tempData.Length )
              {
                nextByte = tempData.ByteAt( bytePos );
              }
            }
            while ( ( bytePos < tempData.Length )
            &&      ( nextByte != 34 ) );
            if ( nextByte == 34 )
            {
              stringLiteral += Types.ConstantData.PetSCIIToChar[nextByte].CharValue;
              info.LineData.AppendU8( nextByte );
            }
            Token basicToken = new Token();
            basicToken.TokenType  = Token.Type.STRING_LITERAL;
            basicToken.Content    = stringLiteral;
            basicToken.StartIndex = startIndex;
            info.Tokens.Add( basicToken );

            ++bytePos;
            continue;
          }
          if ( insideDataStatement )
          {
            // innerhalb von Data keine Token-Aufschlüsselung
            if ( nextByte == ':' )
            {
              insideDataStatement = false;
            }

            AddDirectToken( info, nextByte, bytePos );
            ++bytePos;
            continue;
          }
          if ( nextByte == 0x3f )
          {
            // ? durch PRINT ersetzen
            nextByte = 0x99;
            info.LineData.AppendU8( nextByte );

            Token basicToken = new Token();
            basicToken.TokenType = Token.Type.BASIC_TOKEN;
            basicToken.ByteValue = nextByte;
            basicToken.Content = "" + Types.ConstantData.PetSCIIToChar[0x3f].CharValue;
            basicToken.StartIndex = bytePos;
            info.Tokens.Add( basicToken );
            ++bytePos;
            continue;
          }
          if ( ( nextByte >= 0x30 )
          &&   ( nextByte < 0x3c ) )
          {
            // numerisch bzw. Klammern?, direkt einsetzen
            AddDirectToken( info, nextByte, bytePos );
            ++bytePos;
            continue;
          }
          // is there a token now?
          bool entryFound = true;
          foreach ( KeyValuePair<byte, Opcode> opcodeEntry in m_OpcodesFromByte )
          {
            Opcode  opcode = opcodeEntry.Value;

            entryFound = true;
            for ( int i = 0; i < opcode.Command.Length; ++i )
            {
              if ( bytePos + i >= tempData.Length )
              {
                // can't be this token
                entryFound = false;
                break;
              }
              if ( tempData.ByteAt( bytePos + i ) != (byte)opcode.Command[i] )
              {
                entryFound = false;
                break;
              }
            }
            if ( entryFound )
            {
              Token basicToken = new Token();
              basicToken.TokenType = Token.Type.BASIC_TOKEN;
              basicToken.ByteValue = (byte)opcode.ByteValue;
              basicToken.Content = opcode.Command;
              basicToken.StartIndex = bytePos;
              info.Tokens.Add( basicToken );

              info.LineData.AppendU8( (byte)opcode.ByteValue );
              bytePos += opcode.Command.Length;

              insideDataStatement = ( opcode.Command == "DATA" );
              if ( opcode.Command == "REM" )
              {
                insideREMStatement = true;
              }
              break;
            }
          }
          if ( entryFound )
          {
            continue;
          }
          // is it an extended token?
          entryFound = true;
          foreach ( KeyValuePair<string, Opcode> opcodeEntry in m_ExOpcodes )
          {
            Opcode opcode = opcodeEntry.Value;

            entryFound = true;
            for ( int i = 0; i < opcode.Command.Length; ++i )
            {
              if ( bytePos + i >= tempData.Length )
              {
                // can't be this token
                entryFound = false;
                break;
              }
              if ( tempData.ByteAt( bytePos + i ) != (byte)opcode.Command[i] )
              {
                entryFound = false;
                break;
              }
            }
            if ( entryFound )
            {
              Token basicToken = new Token();
              basicToken.TokenType = Token.Type.EX_BASIC_TOKEN;
              basicToken.ByteValue = (byte)opcode.ByteValue;
              basicToken.Content = opcode.Command;
              basicToken.StartIndex = bytePos;
              info.Tokens.Add( basicToken );

              info.LineData.AppendU8( (byte)opcode.ByteValue );
              bytePos += opcode.Command.Length;

              insideDataStatement = false;
              break;
            }
          }
          if ( entryFound )
          {
            continue;
          }
          // not a token, add directly
          AddDirectToken( info, nextByte, bytePos );
          ++bytePos;
          continue;
        }
        else if ( nextByte == 0xff )
        {
          // PI
          AddDirectToken( info, nextByte, bytePos );
          ++bytePos;
          continue;
        }
        else
        {
          ++bytePos;
          continue;
        }
      }

      if ( info.LineData.Length > 250 )
      {
        AddError( LineIndex, Types.ErrorCode.E3006_BASIC_LINE_TOO_LONG, "Line is too long, max. 250 bytes possible" );
      }
      else if ( info.LineData.Length > 80 )
      {
        AddWarning( LineIndex, Types.ErrorCode.W1001_BASIC_LINE_TOO_LONG_FOR_MANUAL_ENTRY, "Line is too long for manual entry", 0, info.Line.Length );
      }

      // update line number references
      for ( int i = 0; i < info.Tokens.Count; ++i )
      {
        Token token = info.Tokens[i];

        if ( ( token.TokenType == Token.Type.BASIC_TOKEN )
        &&   ( ( token.ByteValue == 0x89 )      // GOTO
        ||     ( token.ByteValue == 0x8d ) ) )  // GOSUB
        {
          // look up next token, is it a line number? (spaces are ignored)
          int nextTokenIndex = FindNextToken( info.Tokens, i );
          while ( ( nextTokenIndex != -1 )
          && ( nextTokenIndex < info.Tokens.Count ) )
          {
            if ( info.Tokens[nextTokenIndex].TokenType == Token.Type.NUMERIC_LITERAL )
            {
              int referencedLineNumber = GR.Convert.ToI32( info.Tokens[nextTokenIndex].Content );

              info.ReferencedLineNumbers.Add( referencedLineNumber );
              ++nextTokenIndex;
              if ( nextTokenIndex >= info.Tokens.Count )
              {
                break;
              }
              if ( info.Tokens[nextTokenIndex].Content != "," )
              {
                break;
              }
            }
            ++nextTokenIndex;
          }
        }
        if ( ( token.TokenType == Token.Type.BASIC_TOKEN )
        &&   ( ( token.ByteValue == 0xa7 )        // THEN
        ||     ( token.ByteValue == 0x8a ) ) )  // RUN
        {
          // only one line number
          // look up next token, is it a line number? (spaces are ignored)
          int nextTokenIndex = FindNextToken( info.Tokens, i );
          if ( ( nextTokenIndex != -1 )
          && ( nextTokenIndex < info.Tokens.Count ) )
          {
            if ( info.Tokens[nextTokenIndex].TokenType == Token.Type.NUMERIC_LITERAL )
            {
              int referencedLineNumber = GR.Convert.ToI32( info.Tokens[nextTokenIndex].Content );

              info.ReferencedLineNumbers.Add( referencedLineNumber );
            }
          }
        }
      }
      return info;
    }



    private void ProcessLines( string[] lines, bool LabelMode )
    {
      int lineIndex = -1;
      int lastLineNumber = -1;
      foreach ( string lineArg in lines )
      {
        ++lineIndex;
        if ( lineArg.Length == 0 )
        {
          continue;
        }
        string line = lineArg.Trim();
        if ( line.Length == 0 )
        {
          continue;
        }

        var info = TokenizeLine( line, lineIndex, ref lastLineNumber );



        m_LineInfos[info.LineIndex] = info;
      }
    }



    public override bool Assemble( CompileConfig Config )
    {
      GR.Memory.ByteBuffer result = new GR.Memory.ByteBuffer();

      int     startAddress = Config.StartAddress;
      if ( startAddress == -1 )
      {
        startAddress = 0x0801;
      }

      result.AppendU16( (ushort)startAddress );

      int     curAddress = startAddress;
      foreach ( LineInfo info in m_LineInfos.Values )
      {
        // pointer to next line
        result.AppendU16( (ushort)( curAddress + info.LineData.Length + 5 ) );
        result.AppendU16( (ushort)info.LineNumber );
        result.Append( info.LineData );
        // end of line
        result.AppendU8( 0 );

        curAddress += (int)info.LineData.Length + 5;
      }
      result.AppendU16( 0 );

      int     originalSize = (int)result.Length - 2;


      //Debug.Log( "Compiled: " + result.ToString() );

      string    outputPureFilename = "HURZ";
      try
      {
        outputPureFilename = System.IO.Path.GetFileNameWithoutExtension( Config.OutputFile );
      }
      catch ( Exception )
      {
        // arghh exceptions!
      }
      int     fileStartAddress = -1;

      AssembledOutput = new AssemblyOutput();
      AssembledOutput.Assembly = result;
      if ( Config.TargetType == Types.CompileTargetType.T64 )
      {
        Formats.T64 t64 = new C64Studio.Formats.T64();

        Formats.T64.FileRecord  record = new C64Studio.Formats.T64.FileRecord();

        record.Filename = Util.ToFilename( outputPureFilename );
        record.StartAddress = (ushort)fileStartAddress;

        t64.TapeInfo.Description = "C64S tape file\r\nDemo tape";
        t64.TapeInfo.UserDescription = "USERDESC";
        t64.FileRecords.Add( record );
        t64.FileDatas.Add( result );

        AssembledOutput.Assembly = t64.Compile();
      }
      else if ( Config.TargetType == Types.CompileTargetType.D64 )
      {
        Formats.D64 d64 = new C64Studio.Formats.D64();

        d64.CreateEmptyMedia();

        GR.Memory.ByteBuffer    bufName = Util.ToFilename( outputPureFilename );
        d64.WriteFile( bufName, AssembledOutput.Assembly, C64Studio.Types.FileType.PRG );

        AssembledOutput.Assembly = d64.Compile();
      }
      else if ( Config.TargetType == Types.CompileTargetType.TAP )
      {
        Formats.Tap tap = new C64Studio.Formats.Tap();

        tap.WriteFile( Util.ToFilename( outputPureFilename ), AssembledOutput.Assembly, C64Studio.Types.FileType.PRG );
        AssembledOutput.Assembly = tap.Compile();
      }
      else if ( ( Config.TargetType == Types.CompileTargetType.CARTRIDGE_8K_BIN )
      ||        ( Config.TargetType == Types.CompileTargetType.CARTRIDGE_8K_CRT ) )
      {
        if ( result.Length < 8192 )
        {
          // fill up
          AssembledOutput.Assembly = result + new GR.Memory.ByteBuffer( 8192 - result.Length );
        }
      }
      else if ( ( Config.TargetType == Types.CompileTargetType.CARTRIDGE_16K_BIN )
      ||        ( Config.TargetType == Types.CompileTargetType.CARTRIDGE_16K_CRT ) )
      {
        if ( result.Length < 16384 )
        {
          // fill up
          result = result + new GR.Memory.ByteBuffer( 16384 - result.Length );
        }
        if ( Config.TargetType == Types.CompileTargetType.CARTRIDGE_16K_CRT )
        {
          // build cartridge header
          GR.Memory.ByteBuffer    header = new GR.Memory.ByteBuffer();

          header.AppendHex( "43363420434152545249444745202020" ); // "C64 CARTRIDGE   "
          header.AppendU32NetworkOrder( 0x40 );
          header.AppendU16NetworkOrder( 0x0100 );
          header.AppendU16( 0 );
          header.AppendU8( 0 );
          header.AppendU8( 0 );

          // reserved
          header.AppendU8( 0 );
          header.AppendU8( 0 );
          header.AppendU8( 0 );
          header.AppendU8( 0 );
          header.AppendU8( 0 );
          header.AppendU8( 0 );   

          // cartridge name
          string name = System.IO.Path.GetFileNameWithoutExtension( m_CompileTargetFile ).ToUpper();

          if ( name.Length > 32 )
          {
            name = name.Substring( 0, 32 );
          }
          while ( name.Length < 32 )
          {
            name += (char)0;
          }
          foreach ( char aChar in name )
          {
            header.AppendU8( (byte)aChar );
          }

          GR.Memory.ByteBuffer chip = new GR.Memory.ByteBuffer();

          chip.AppendHex( "43484950" );   // chip
          uint length = 16 + result.Length;
          chip.AppendU32NetworkOrder( length );
          chip.AppendU16( 0 );  // ROM
          chip.AppendU16( 0 );  // Bank number
          chip.AppendU16NetworkOrder( 0x8000 ); // loading start address
          chip.AppendU16NetworkOrder( 0x4000 ); // rom size

          chip.Append( result );

          AssembledOutput.Assembly = header + chip;
        }
      }
      AssembledOutput.OriginalAssemblyStartAddress  = startAddress;
      AssembledOutput.OriginalAssemblySize          = originalSize;
      return true;
    }



    public override bool DocumentAndLineFromGlobalLine( int GlobalLine, out string DocumentFile, out int DocumentLine )
    {
      DocumentLine = GlobalLine;
      DocumentFile = m_Filename;
      return true;
    }



    private string DecompileStringLiteral( string Literal )
    {
      // replace value with char
      StringBuilder sbTemp = new StringBuilder();

      int     charPos = 0;
      bool    insideTag = false;

      while ( charPos < Literal.Length )
      {
        char  value = Literal[charPos];
        if ( value == '{' )
        {
          insideTag = true;
          sbTemp.Append( value );
        }
        else if ( value == '}' )
        {
          sbTemp.Append( value );
          insideTag = false;
        }
        else if ( insideTag )
        {
          sbTemp.Append( value );
        }
        else
        {
          char replacement = Types.ConstantData.PetSCIIToChar[(byte)value].CharValue;
          sbTemp.Append( replacement );
        }
        ++charPos;
      }

      return sbTemp.ToString();
    }



    public string EncodeToLabels()
    {
      StringBuilder sb = new StringBuilder();
      GR.Collections.Map<int,string>     lineNumberReference = new GR.Collections.Map<int, string>();
      foreach ( KeyValuePair<int,LineInfo> lineInfo in m_LineInfos )
      {
        if ( lineInfo.Value.ReferencedLineNumbers.Count > 0 )
        {
          foreach ( int number in lineInfo.Value.ReferencedLineNumbers )
          {
            if ( !lineNumberReference.ContainsKey( number ) )
            {
              string    newLabel = "LABEL" + number.ToString();
              lineNumberReference[number] = newLabel;
            }
          }
        }
      }

      foreach ( KeyValuePair<int,LineInfo> lineInfo in m_LineInfos )
      {
        if ( lineNumberReference.ContainsKey( lineInfo.Value.LineNumber ) )
        {
          // something is referencing this line
          sb.AppendLine();
          sb.Append( lineNumberReference[lineInfo.Value.LineNumber] + "\r\n" );
        }
        bool  hadREM = false;

        for ( int i = 0; i < lineInfo.Value.Tokens.Count; ++i )
        {
          Token token = lineInfo.Value.Tokens[i];
          if ( token.TokenType == Token.Type.LINE_NUMBER )
          {
            continue;
          }
          /*
          // TODO - only use if we can re-merge single tokens from label mode
          if ( ( token.TokenType == Token.Type.DIRECT_TOKEN )
          &&   ( token.Content == ":" ) )
          {
            sb.AppendLine();
            continue;
          }
           */

          if ( token.TokenType == Token.Type.BASIC_TOKEN )
          {
            if ( token.ByteValue == m_Opcodes["REM"].ByteValue )
            {
              hadREM = true;
            }

            if ( ( token.ByteValue == m_Opcodes["RUN"].ByteValue )
            ||   ( token.ByteValue == m_Opcodes["THEN"].ByteValue ) )
            {
              // insert label instead of line number
              if ( i + 1 < lineInfo.Value.Tokens.Count )
              {
                int     refNo = -1;
                if ( int.TryParse( lineInfo.Value.Tokens[i + 1].Content, out refNo ) )
                {
                  if ( !lineNumberReference.ContainsKey( refNo ) )
                  {
                    Debug.Log( "Error, found linenumber without reference " + refNo + " in line " + lineInfo.Value.LineNumber );
                    ++i;
                    continue;
                  }

                  sb.Append( token.Content + " " + lineNumberReference[refNo].ToString() );
                  ++i;
                  continue;
                }
              }
            }
            if ( ( token.ByteValue == m_Opcodes["GOTO"].ByteValue )
            ||   ( token.ByteValue == m_Opcodes["GOSUB"].ByteValue ) )
            {
              // ON x GOTO/GOSUB can have more than one line number
              // insert label instead of line number
              //sb.Append( token.Content + " " );
              sb.Append( token.Content );

              int nextIndex = i + 1;
              bool mustBeComma = false;
              while ( nextIndex < lineInfo.Value.Tokens.Count )
              {
                Token nextToken = lineInfo.Value.Tokens[nextIndex];
                if ( !mustBeComma )
                {
                  if ( nextToken.TokenType == Token.Type.NUMERIC_LITERAL )
                  {
                    // numeric!
                    int refNo = GR.Convert.ToI32( nextToken.Content );
                    if ( !lineNumberReference.ContainsKey( refNo ) )
                    {
                      Debug.Log( "Error, found linenumber without reference " + refNo + " in line " + lineInfo.Value.LineNumber );
                      continue;
                    }
                    sb.Append( lineNumberReference[refNo].ToString() );
                  }
                  else if ( ( nextToken.TokenType == Token.Type.DIRECT_TOKEN )
                  &&        ( nextToken.Content == " " ) )
                  {
                    if ( !Settings.StripSpaces )
                    {
                      sb.Append( nextToken.Content );
                    }
                    ++nextIndex;
                    continue;
                  }
                  else
                  {
                    // error or end
                    break;
                  }
                  mustBeComma = true;
                }
                else
                {
                  if ( ( nextToken.TokenType != Token.Type.DIRECT_TOKEN )
                  ||   ( nextToken.ByteValue != ',' ) )
                  {
                    // error or end, not a comma
                    --nextIndex;
                    break;
                  }
                  sb.Append( ',' );
                  mustBeComma = false;
                }
                ++nextIndex;
              }
              i = nextIndex;
              continue;
            }
          }
          // if we got here there was no label inserted
          sb.Append( token.Content );
          if ( ( token.TokenType == Token.Type.BASIC_TOKEN )
          ||   ( token.TokenType == Token.Type.NUMERIC_LITERAL )
          ||   ( token.TokenType == Token.Type.EX_BASIC_TOKEN ) )
          {
            if ( ( token.ByteValue != m_Opcodes["REM"].ByteValue )
            &&   ( hadREM ) )
            {
              sb.Append( " " );
            }
          }
        }
        sb.AppendLine();
      }
      return sb.ToString();
    }



    // finds next non-space token or returns -1
    private int FindNextToken( List<Token> Tokens, int StartIndex )
    {
      int curIndex = StartIndex + 1;

      while ( curIndex < Tokens.Count )
      {
        if ( ( Tokens[curIndex].TokenType != Token.Type.DIRECT_TOKEN )
        ||   ( Tokens[curIndex].ByteValue != 0x20 ) )
        {
          // not a Space
          return curIndex;
        }
        ++curIndex;
      }
      return -1;
    }



    public string DecodeFromLabels()
    {
      StringBuilder sb = new StringBuilder();
      GR.Collections.Map<string,int>     labelToNumber = new GR.Collections.Map<string, int>();

      int     lineNumber = 10;

      // collect labels
      foreach ( KeyValuePair<int,LineInfo> lineInfo in m_LineInfos )
      {
        if ( ( lineInfo.Value.Tokens.Count > 0 )
        &&   ( lineInfo.Value.Tokens[0].TokenType == Token.Type.EX_BASIC_TOKEN )
        &&   ( lineInfo.Value.Tokens[0].ByteValue == m_ExOpcodes["LABEL"].ByteValue ) )
        {
          // skip label definitions
          if ( lineInfo.Value.Tokens.Count > 1 )
          {
            string labelToReplace = "LABEL" + lineInfo.Value.Tokens[1].Content;

            labelToNumber[labelToReplace] = lineNumber;
            //Debug.Log( "Replace label " + labelToReplace + " with line " + lineNumber );
          }
          else
          {
            AddError( lineInfo.Value.LineIndex, Types.ErrorCode.E3003_BASIC_LABEL_MALFORMED, "Label malformed" );
          }
          continue;
        }
        lineNumber += 10;
      }
      lineNumber = 10;
      foreach ( KeyValuePair<int, LineInfo> lineInfo in m_LineInfos )
      {
        bool    hadREM = false;

        // is this a label definition?
        int nextTokenIndex = FindNextToken( lineInfo.Value.Tokens, -1 );
        int nextTokenIndex2 = FindNextToken( lineInfo.Value.Tokens, nextTokenIndex );
        int nextTokenIndex3 = FindNextToken( lineInfo.Value.Tokens, nextTokenIndex2 );
        if ( ( nextTokenIndex != -1 )
        &&   ( nextTokenIndex2 != -1 )
        &&   ( nextTokenIndex3 == -1 ) )
        {
          if ( ( lineInfo.Value.Tokens[nextTokenIndex].TokenType == Token.Type.EX_BASIC_TOKEN )
          &&   ( lineInfo.Value.Tokens[nextTokenIndex].ByteValue == m_ExOpcodes["LABEL"].ByteValue )
          &&   ( lineInfo.Value.Tokens[nextTokenIndex2].TokenType == Token.Type.NUMERIC_LITERAL ) )
          {
            // a label definition
            continue;
          }
        }

        sb.Append( lineNumber.ToString() + " " );
        for ( int tokenIndex = 0; tokenIndex < lineInfo.Value.Tokens.Count; ++tokenIndex  )
        {

          Token token = lineInfo.Value.Tokens[tokenIndex];

          if ( ( token.TokenType == Token.Type.DIRECT_TOKEN )
          &&   ( Settings.StripSpaces )
          &&   ( !hadREM )
          &&   ( token.Content.Trim().Length == 0 ) )
          {
            continue;
          }
          bool    tokenIsInserted = false;

          if ( ( token.TokenType == Token.Type.BASIC_TOKEN )
          &&   ( token.ByteValue == m_Opcodes["REM"].ByteValue ) )
          {
            hadREM = true;
          }


          if ( ( token.TokenType == Token.Type.BASIC_TOKEN )
          &&   ( ( token.ByteValue == m_Opcodes["GOTO"].ByteValue )
          ||     ( token.ByteValue == m_Opcodes["GOSUB"].ByteValue )
          ||     ( token.ByteValue == m_Opcodes["THEN"].ByteValue )
          ||     ( token.ByteValue == m_Opcodes["RUN"].ByteValue ) ) )
          {
            bool    isGotoOrGosub = ( token.ByteValue == m_Opcodes["GOTO"].ByteValue ) | ( token.ByteValue == m_Opcodes["GOSUB"].ByteValue );
            nextTokenIndex = FindNextToken( lineInfo.Value.Tokens, tokenIndex );
            nextTokenIndex2 = FindNextToken( lineInfo.Value.Tokens, nextTokenIndex );

            while ( ( nextTokenIndex != -1 )
            &&      ( nextTokenIndex2 != -1 ) )
            {
              if ( ( lineInfo.Value.Tokens[nextTokenIndex].TokenType == Token.Type.EX_BASIC_TOKEN )
              &&   ( lineInfo.Value.Tokens[nextTokenIndex].ByteValue == m_ExOpcodes["LABEL"].ByteValue )
              &&   ( lineInfo.Value.Tokens[nextTokenIndex2].TokenType == Token.Type.NUMERIC_LITERAL ) )
              {
                string label = "LABEL" + lineInfo.Value.Tokens[nextTokenIndex2].Content;

                if ( !labelToNumber.ContainsKey( label ) )
                {
                  AddError( lineInfo.Value.LineIndex, Types.ErrorCode.E3004_BASIC_MISSING_LABEL, "Unknown label " + label + " encountered" );
                }
                else
                {
                  if ( !tokenIsInserted )
                  {
                    tokenIsInserted = true;
                    sb.Append( token.Content );

                    while ( nextTokenIndex - 1 > tokenIndex )
                    {
                      // there were blanks in between
                      sb.Append( lineInfo.Value.Tokens[tokenIndex + 1].Content );
                      ++tokenIndex;
                    }
                  }
                  sb.Append( labelToNumber[label].ToString() );
                }
                tokenIndex = nextTokenIndex2;
                if ( !isGotoOrGosub )
                {
                  break;
                }
                nextTokenIndex = FindNextToken( lineInfo.Value.Tokens, nextTokenIndex2 );
                if ( ( nextTokenIndex == -1 )
                ||   ( lineInfo.Value.Tokens[nextTokenIndex].Content != "," ) )
                {
                  break;
                }
                sb.Append( ',' );
                nextTokenIndex = FindNextToken( lineInfo.Value.Tokens, nextTokenIndex );
                nextTokenIndex2 = FindNextToken( lineInfo.Value.Tokens, nextTokenIndex );
              }
              else
              {
                break;
              }
            }
          }

          if ( token.TokenType == Token.Type.STRING_LITERAL )
          {
            if ( ( token.Content.StartsWith( "\"" ) )
            &&   ( token.Content.EndsWith( "\"" ) ) )
            {
              // replace value with char
              //token.Content = DecompileStringLiteral( token.Content );
            }
          }

          if ( !tokenIsInserted )
          {
            sb.Append( token.Content );
          }
        }
        sb.Append( "\r\n" );
        lineNumber += 10;
      }
      /*
      foreach ( KeyValuePair<int,LineInfo> lineInfo in m_LineInfos )
      {
        if ( lineInfo.Value.ReferencedLineNumbers.Count > 0 )
        {
          foreach ( int number in lineInfo.Value.ReferencedLineNumbers )
          {
            if ( !lineNumberReference.ContainsKey( number ) )
            {
              string    newLabel = "label_" + number.ToString();
              lineNumberReference[number] = newLabel;
            }
          }
        }
      }*/
      return sb.ToString();
    }



    public static bool Disassemble( GR.Memory.ByteBuffer Data, out List<string> Lines  )
    {
      Lines = new List<string>();
      if ( ( Data == null )
      ||   ( Data.Length == 0 ) )
      {
        return false;
      }
      

      // Zeile =
      //  2 Bytes pointer auf nächste Zeile
      //  2 Bytes Zeilennummer
      //  x Bytes Zeileninhalt
      //  1 Byte 00 Zeilenende
      //  zum Abschluss ist der Pointer 00 00

      int     dataPos = 0;

      while ( dataPos < Data.Length )
      {
        // pointer to next line
        if ( dataPos + 2 > Data.Length )
        {
          Debug.Log( "no space for pointer" );
          return false;
        }
        if ( Data.UInt16At( dataPos ) == 0 )
        {
          // end
          Debug.Log( "end reached" );
          return true;
        }

        // TODO - check pointer?
        dataPos += 2;

        // line number
        if ( dataPos + 2 > Data.Length )
        {
          Debug.Log( "no space for line number" );
          return false;
        }
        ushort lineNumber = Data.UInt16At( dataPos );

        dataPos += 2;

        string    lineContent = lineNumber.ToString() + " ";
        bool      insideStringLiteral = false;
        bool      encounteredREM = false;

        while ( ( dataPos < Data.Length )
        &&      ( Data.ByteAt( dataPos ) != 0 ) )
        {
          byte    byteValue = Data.ByteAt( dataPos );
          if ( encounteredREM )
          {
            if ( ( byteValue >= 192 )
            &&   ( byteValue <= 223 ) )
            {
              byteValue -= 192 - 96;
            }
            if ( ( byteValue >= 224 )
            &&   ( byteValue <= 254 ) )
            {
              byteValue -= 224 - 160;
            }
            if ( byteValue == 255 )
            {
              byteValue = 126;
            }

            // REM is only remark, no opcode parsing anymore
            if ( m_OpcodesFromByte.ContainsKey( byteValue ) )
            {
              lineContent += m_OpcodesFromByte[byteValue].Command;
            }
            else if ( Types.ConstantData.PETSCIIToUnicode.ContainsKey( byteValue ) )
            {
              char charToUse = Types.ConstantData.PETSCIIToUnicode[byteValue];
              lineContent += charToUse;
            }
            ++dataPos;
            continue;
          }

          if ( byteValue == 34 )
          {
            insideStringLiteral = !insideStringLiteral;
          }
          if ( insideStringLiteral )
          {
            //if ( KeymapEntryExists( System.Windows.Forms.InputLanguage.CurrentInputLanguage, System.Windows.Forms.Keys
            // Codes 192-223 wie Codes  96-127
            // Codes 224-254 wie Codes 160-190
            // Code  255     wie Code  126
            if ( ( byteValue >= 192 )
            &&   ( byteValue <= 223 ) )
            {
              byteValue -= 192 - 96;
            }
            if ( ( byteValue >= 224 )
            &&   ( byteValue <= 254 ) )
            {
              byteValue -= 224 - 160;
            }
            if ( byteValue == 255 )
            {
              byteValue = 126;
            }

            var c64Key = Types.ConstantData.FindC64KeyByPETSCII( byteValue );
            if ( c64Key != null )
            {
              lineContent += c64Key.CharValue;
            }
            else
            {
              Debug.Log( "Unknown byte value " + byteValue );
            }
          }
          else
          {
            if ( ( !m_OpcodesFromByte.ContainsKey( byteValue ) )
            ||   ( m_OpcodesFromByte[byteValue].Command.StartsWith( "{" ) ) )
            {
              //if ( KeymapEntryExists( System.Windows.Forms.InputLanguage.CurrentInputLanguage, System.Windows.Forms.Keys
              if ( Types.ConstantData.PETSCIIToUnicode.ContainsKey( byteValue ) )
              {
                char charToUse = Types.ConstantData.PETSCIIToUnicode[byteValue];
                /*
                if ( KeyMap.KeymapEntryExists( (System.Windows.Forms.Keys)charToUse ) )
                {
                  charToUse = KeyMap.GetKeymapEntry( (System.Windows.Forms.Keys)charToUse ).Normal;
                }*/
                lineContent += charToUse;
              }
              else if ( ActionTokenByByteValue.ContainsKey( byteValue ) )
              {
                lineContent += ActionTokenByByteValue[byteValue].Replacement;
              }
              else
              {
                Debug.Log( "Unknown byte value " + byteValue );
              }
            }
            else
            {
              if ( m_OpcodesFromByte[byteValue].ByteValue == 0x8F )
              {
                encounteredREM = true;
              }
              lineContent += m_OpcodesFromByte[byteValue].Command;
            }
          }
          ++dataPos;
        }

        if ( dataPos >= Data.Length )
        {
          // reached the end, should not happen!
          Debug.Log( "reached wrong end" );
          return false;
        }
        Debug.Log( "Line:" + lineContent );
        Lines.Add( lineContent );
        ++dataPos;
      }

      return false;
    }



    public RenumberResult CanRenumber( int LineStart, int LineStep )
    {
      if ( m_LineInfos.Count == 0 )
      {
        return RenumberResult.NOTHING_TO_DO;
      }
      if ( LineStart + LineStep * ( m_LineInfos.Count - 1 ) >= 64000 )
      {
        return RenumberResult.TOO_MANY_LINES;
      }
      return RenumberResult.OK;
    }



    public string Renumber( int LineStart, int LineStep )
    {
      StringBuilder sb = new StringBuilder();

      GR.Collections.Map<int, int> lineNumberReference = new GR.Collections.Map<int, int>();

      int curLine = LineStart;
      foreach ( KeyValuePair<int, LineInfo> lineInfo in m_LineInfos )
      {
        lineNumberReference[lineInfo.Value.LineNumber] = curLine;

        curLine += LineStep;
      }

      curLine = LineStart;

      int     firstLineIndex = -1;
      foreach ( KeyValuePair<int,LineInfo> lineInfoOrig in m_LineInfos )
      {
        ++firstLineIndex;
        // keep empty lines
        while ( firstLineIndex < lineInfoOrig.Key )
        {
          sb.Append( "\r\n" );
          ++firstLineIndex;
        }
        //var lineInfo = PureTokenizeLine( lineInfoOrig.Value.Line, lineInfoOrig.Value.LineNumber );
        var lineInfo = TokenizeLine( lineInfoOrig.Value.Line, 0, ref curLine );
        for ( int i = 0; i < lineInfo.Tokens.Count; ++i )
        {
          Token token = lineInfo.Tokens[i];
          if ( token.TokenType == Token.Type.LINE_NUMBER )
          {
            sb.Append( lineNumberReference[lineInfo.LineNumber] );
            continue;
          }
          if ( token.TokenType == Token.Type.BASIC_TOKEN )
          {
            if ( ( token.ByteValue == m_Opcodes["RUN"].ByteValue )
            ||   ( token.ByteValue == m_Opcodes["THEN"].ByteValue ) )
            {
              // insert label instead of line number
              if ( i + 1 < lineInfo.Tokens.Count )
              {
                int     refNo = -1;
                int     nextTokenIndex = FindNextToken( lineInfo.Tokens, i );
                if ( ( nextTokenIndex != -1 )
                &&   ( int.TryParse( lineInfo.Tokens[nextTokenIndex].Content, out refNo ) ) )
                {
                  sb.Append( token.Content );

                  while ( i + 1 < nextTokenIndex )
                  {
                    sb.Append( lineInfo.Tokens[i + 1].Content );
                    ++i;
                  }
                  sb.Append( lineNumberReference[refNo] );
                  ++i;
                  continue;
                }
              }
            }
            if ( ( token.ByteValue == m_Opcodes["GOTO"].ByteValue )
            ||   ( token.ByteValue == m_Opcodes["GOSUB"].ByteValue ) )
            {
              // ON x GOTO/GOSUB can have more than one line number
              // insert label instead of line number
              sb.Append( token.Content );
              int nextIndex = i + 1;
              bool mustBeComma = false;
              while ( nextIndex < lineInfo.Tokens.Count )
              {
                Token nextToken = lineInfo.Tokens[nextIndex];
                if ( !mustBeComma )
                {
                  if ( nextToken.TokenType == Token.Type.NUMERIC_LITERAL )
                  {
                    // numeric!
                    int refNo = GR.Convert.ToI32( nextToken.Content );
                    if ( lineNumberReference.ContainsKey( refNo ) )
                    {
                      sb.Append( lineNumberReference[refNo].ToString() );
                    }
                    else
                    {
                      // no reference found, keep old value
                      sb.Append( nextToken.Content );
                    }
                    ++nextIndex;
                  }
                  else if ( ( nextToken.TokenType == Token.Type.DIRECT_TOKEN )
                  &&        ( nextToken.Content == " " ) )
                  {
                    // space is valid, skip
                    ++nextIndex;
                    continue;
                  }
                  else
                  {
                    // error or end
                    break;
                  }
                  mustBeComma = true;
                }
                else
                {
                  if ( ( nextToken.TokenType != Token.Type.DIRECT_TOKEN )
                  ||   ( nextToken.ByteValue != ',' ) )
                  {
                    // error or end, not a comma
                    break;
                  }
                  sb.Append( nextToken.Content );
                  mustBeComma = false;
                  ++nextIndex;
                }
              }
              i = nextIndex - 1;
              continue;
            }
          }
          // if we got here there was no label inserted
          sb.Append( token.Content );
          if ( ( token.TokenType == Token.Type.BASIC_TOKEN )
          ||   ( token.TokenType == Token.Type.EX_BASIC_TOKEN ) )
          {
            if ( token.ByteValue != m_Opcodes["REM"].ByteValue )
            {
              //sb.Append( " " );
            }
          }
        }
        sb.Append( "\r\n" );
        //sb.Append( lineInfo.Value.Line + "\r\n" );
        // TODO - replace goto/numbers with label
      }

      // strip last line break
      if ( m_LineInfos.Count > 0 )
      {
        sb.Remove( sb.Length - 2, 2 );
      }
      return sb.ToString();
    }



    public string ReplaceAllSymbolsByMacros( string BasicText )
    {
      for ( int i = 0; i < BasicText.Length; ++i )
      {
        char    chartoCheck = BasicText[i];

        if ( chartoCheck > (char)255 )
        {
          var c64Key = Types.ConstantData.FindC64KeyByUnicode( chartoCheck );
          if ( c64Key != null )
          {
            if ( c64Key.Replacements.Count > 0 )
            {
              string    replacement = c64Key.Replacements[0];

              BasicText = BasicText.Substring( 0, i ) + "{" + replacement + "}" + BasicText.Substring( i + 1 );
              i += replacement.Length - 1;
            }
          }
        }
      }
      return BasicText;
    }



    public string ReplaceAllMacrosBySymbols( string BasicText, out bool HadError )
    {
      StringBuilder     sb = new StringBuilder();

      int               posInLine = 0;
      int               macroStartPos = 0;
      bool              insideMacro = false;

      HadError = false;

      while ( posInLine < BasicText.Length )
      {
        char    curChar = BasicText[posInLine];
        if ( insideMacro )
        {
          if ( curChar == '}' )
          {
            insideMacro = false;

            string macro = BasicText.Substring( macroStartPos + 1, posInLine - macroStartPos - 1 ).ToUpper();
            int macroCount = 1;

            macro = Parser.BasicFileParser.DetermineMacroCount( macro, out macroCount );

            bool  foundMacro = false;
            foreach ( var key in Types.ConstantData.AllPhysicalKeyInfos )
            {
              if ( key.Replacements.Contains( macro ) )
              {
                for ( int i = 0; i < macroCount; ++i )
                {
                  sb.Append( key.CharValue );
                }
                foundMacro = true;
                break;
              }
            }
            if ( !foundMacro )
            {
              Debug.Log( "Unknown macro " + macro );
              HadError = true;
              return null;
            }
          }
          ++posInLine;
          continue;
        }
        if ( curChar == '{' )
        {
          insideMacro = true;
          macroStartPos = posInLine;
          ++posInLine;
          continue;
        }
        // normal chars are passed on (also tabs, cr, lf)
        sb.Append( curChar );
        ++posInLine;
      }
      return sb.ToString();
    }

  }
}
