﻿using RetroDevStudio;
using System;
using System.Collections.Generic;
using System.Text;

namespace C64Studio.Tasks
{
  /// <summary>
  /// preparses a single file (without project) and populates hover and label info
  /// </summary>
  public class TaskParseFile : Task
  {
    private DocumentInfo    m_Document;
    private ProjectConfig   m_Configuration;



    public TaskParseFile( DocumentInfo Document, ProjectConfig Configuration )
    {
      m_Document = Document;
      m_Configuration = Configuration;
    }



    protected override bool ProcessTask()
    {
      if ( ( m_Document.Type != ProjectElement.ElementType.ASM_SOURCE )
      &&   ( m_Document.Type != ProjectElement.ElementType.BASIC_SOURCE ) )
      {
        return true;
      }

      if ( m_Document.Type == ProjectElement.ElementType.ASM_SOURCE )
      {
        Parser.ASMFileParser parser = new Parser.ASMFileParser();

        var compileConfig = new C64Studio.Parser.CompileConfig();
        if ( m_Document.Element != null )
        {
          compileConfig.Assembler = m_Document.Element.AssemblerType;
        }

        string sourceCode = "";

        if ( m_Document.BaseDoc != null )
        {
          sourceCode = ( (SourceASMEx)m_Document.BaseDoc ).editSource.Text;
        }

        parser.ParseFile( m_Document.FullPath, sourceCode, m_Configuration, compileConfig, null );

        if ( ( compileConfig.Assembler != C64Studio.Types.AssemblerType.AUTO )
        && ( m_Document.BaseDoc != null )
        && ( m_Document.Element != null ) )
        {
          if ( m_Document.Element.AssemblerType != compileConfig.Assembler )
          {
            m_Document.Element.AssemblerType = compileConfig.Assembler;
            m_Document.BaseDoc.SetModified();
          }
        }

        if ( m_Document.BaseDoc != null )
        {
          ( (SourceASMEx)m_Document.BaseDoc ).SetLineInfos( parser.ASMFileInfo );
        }
        var knownTokens = parser.KnownTokens();
        GR.Collections.MultiMap<string, SymbolInfo> knownTokenInfos = parser.KnownTokenInfo();

        m_Document.SetASMFileInfo( parser.ASMFileInfo, knownTokens, knownTokenInfos );
      }
      else if ( m_Document.Type == ProjectElement.ElementType.BASIC_SOURCE )
      {
        Parser.ParserBase parser = Core.DetermineParser( m_Document );

        ( (Parser.BasicFileParser)parser ).SetBasicDialect( ( (Parser.BasicFileParser)parser ).Settings.BASICDialect );
        if ( m_Document.BaseDoc != null )
        {
          ( (Parser.BasicFileParser)parser ).Settings.UpperCaseMode = !( (SourceBasicEx)m_Document.BaseDoc ).m_LowerCaseMode;
        }

        var compileConfig = new C64Studio.Parser.CompileConfig();
        if ( m_Document.Element != null )
        {
          compileConfig.Assembler = m_Document.Element.AssemblerType;
        }

        string sourceCode = "";

        if ( m_Document.BaseDoc != null )
        {
          sourceCode = ( (SourceBasicEx)m_Document.BaseDoc ).editSource.Text;
        }

        parser.ParseFile( m_Document.FullPath, sourceCode, m_Configuration, compileConfig, null );
      }

      var task = new Tasks.TaskUpdateKeywords( m_Document.BaseDoc );
      task.Core = Core;
      task.RunTask();

      return true;
    }
  }
}
