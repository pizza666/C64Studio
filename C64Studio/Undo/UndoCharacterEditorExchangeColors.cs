﻿using System;
using System.Collections.Generic;
using System.Text;
using C64Studio.Controls;
using C64Studio.Formats;
using RetroDevStudio.Types;

namespace C64Studio.Undo
{
  public class UndoCharacterEditorExchangeColors : UndoTask
  {
    private CharacterEditor         Editor = null;
    private ColorType               Color1 = ColorType.BACKGROUND;
    private ColorType               Color2 = ColorType.CUSTOM_COLOR;
    private List<int>               AffectedChars = new List<int>();
    



    public UndoCharacterEditorExchangeColors( CharacterEditor Editor, ColorType Color1, ColorType Color2, List<int> AffectedChars )
    {
      this.Editor = Editor;
      this.Color1 = Color1;
      this.Color2 = Color2;
      this.AffectedChars = AffectedChars;
    }




    public override string Description
    {
      get
      {
        return "Charset Color Exchange";
      }
    }



    public override UndoTask CreateComplementaryTask()
    {
      return new UndoCharacterEditorExchangeColors( Editor, Color1, Color2, AffectedChars );
    }



    public override void Apply()
    {
      Editor.ExchangeColors( Color1, Color2, AffectedChars );
    }
  }
}
