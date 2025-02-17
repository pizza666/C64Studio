﻿using System;
using System.Collections.Generic;
using System.Text;

namespace C64Studio.Types
{
  public class ApplicationEvent
  {
    public enum Type
    {
      NONE = 0,
      DOCUMENT_CREATED,
      DOCUMENT_OPENED,
      DOCUMENT_CLOSED,
      DOCUMENT_ACTIVATED,
      DOCUMENT_FILENAME_CHANGED,
      ELEMENT_CREATED,
      ELEMENT_OPENED,
      ELEMENT_CLOSED,
      ELEMENT_REMOVED,
      ACTIVE_PROJECT_CHANGED,
      SOLUTION_OPENED,
      SOLUTION_CLOSED,
      DOCUMENT_INFO_CREATED,
      DOCUMENT_INFO_REMOVED,
      EMULATOR_LIST_CHANGED,
      KEY_BINDINGS_MODIFIED,
      SOLUTION_RENAMED
    }

    public Type             EventType = Type.NONE;
    public DocumentInfo     Doc = null;
    public ProjectElement   Element = null;
    public Project          Project = null;



    public ApplicationEvent( Type EventType )
    {
      this.EventType = EventType;
    }



    public ApplicationEvent( Type EventType, DocumentInfo Doc )
    {
      this.EventType = EventType;
      this.Doc = Doc;
      
      if ( Doc != null )
      {
        Project = Doc.Project;
        if ( Doc.Element != null )
        {
          this.Element = Doc.Element;
        }
      }
    }



    public ApplicationEvent( Type EventType, ProjectElement Element )
    {
      this.EventType = EventType;
      this.Element  = Element;
      if ( Element != null )
      {
        this.Project  = Element.DocumentInfo.Project;
        this.Doc      = Element.DocumentInfo;
      }
    }



    public ApplicationEvent( Type EventType, Project Project )
    {
      this.EventType = EventType;
      this.Project = Project;
    }
  }
}
