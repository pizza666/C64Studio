﻿using C64Studio.Formats;
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

namespace C64Studio.Controls
{
  public partial class ImportCharscreenFormBase : UserControl
  {
    public StudioCore                   Core = null;



    public ImportCharscreenFormBase()
    {
      InitializeComponent();
    }



    public ImportCharscreenFormBase( StudioCore Core )
    {
      this.Core         = Core;

      InitializeComponent();
    }



    public virtual bool HandleImport( CharsetScreenProject CharScreen, CharsetScreenEditor Editor )
    {
      return false;
    }



  }
}
