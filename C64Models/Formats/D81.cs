﻿using RetroDevStudio;
using System;
using System.Collections.Generic;
using System.Text;

namespace C64Studio.Formats
{
  public class D81 : Disk
  {
    public D81()
    { 
      TRACK_HEADER      = 40;
      SECTOR_HEADER     = 0;

      TRACK_BAM         = 40;
      SECTOR_BAM        = 1;

      TRACK_DIRECTORY   = 40;
      SECTOR_DIRECTORY  = 3;
    }



    public override void CreateEmptyMedia()
    {
      _LastError = "";
      Tracks.Clear();
      for ( int i = 0; i < 80; ++i )
      {
        Tracks.Add( new Track( i + 1, 40 ) );
      }
      CreateBAM();
    }



    void SetDiskName( string Diskname )
    {
      _LastError = "";
      if ( Diskname.Length > 16 )
      {
        Diskname = Diskname.Substring( 0, 16 );
      }
      for ( int i = 0; i < 16; ++i )
      {
        if ( i >= Diskname.Length )
        {
          Tracks[TRACK_HEADER - 1].Sectors[0].Data.SetU8At( 4 + i, 0xa0 );
        }
        else
        {
          Tracks[TRACK_HEADER - 1].Sectors[0].Data.SetU8At( 4 + i, (byte)Diskname[i] );
        }
      }
    }



    void SetDiskID( string DiskID )
    {
      _LastError = "";
      if ( DiskID.Length > 2 )
      {
        DiskID = DiskID.Substring( 0, 2 );
      }

      for ( int i = 0; i < 2; ++i )
      {
        if ( i >= DiskID.Length )
        {
          Tracks[TRACK_HEADER - 1].Sectors[SECTOR_HEADER].Data.SetU8At( 0x16 + i, 0xa0 );
          Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM].Data.SetU8At( 4 + i, 0xa0 );
          Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM + 1].Data.SetU8At( 4 + i, 0xa0 );
        }
        else
        {
          Tracks[TRACK_HEADER - 1].Sectors[SECTOR_HEADER].Data.SetU8At( 0x16 + i, (byte)DiskID[i] );
          Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM].Data.SetU8At( 4 + i, (byte)DiskID[i] );
          Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM + 1].Data.SetU8At( 4 + i, (byte)DiskID[i] );
        }
      }
    }



    private bool IsSectorMarkedAsUsedInBAM( int Track, int Sector )
    {
      _LastError = "";
      if ( ( Track < 1 )
      ||   ( Track > Tracks.Count ) )
      {
        _LastError = "track index out of bounds";
        return false;
      }
      Track track = Tracks[Track - 1];

      if ( ( Sector < 0 )
      ||   ( Sector >= track.Sectors.Count ) )
      {
        _LastError = "sector index out of bounds";
        return false;
      }

      Sector  bam = Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM];
      int     trackOffset = -1;
      if ( Track >= 41 )
      {
        bam = Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM + 1];
        trackOffset = -41;
      }

      // mask out sector
      byte mask = (byte)( 1 << ( Sector & 7 ) );

      if ( ( bam.Data.ByteAt( 16 + ( Track + trackOffset ) * 6 + Sector / 8 + 1 ) & mask ) == 0 )
      {
        return true;
      }
      return false;
    }




    void CreateBAM()
    {
      _LastError = "";

      // Track/Sector of first directory sector
      Track track40 = Tracks[TRACK_HEADER - 1];

      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 0, (byte)TRACK_DIRECTORY );
      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 1, (byte)SECTOR_DIRECTORY );

      // DOS Version
      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 2, 0x44 );

      track40.Sectors[SECTOR_HEADER].Free = false;

      // mark first directory entry as empty
      track40.Sectors[SECTOR_BAM].Data.SetU8At( 0, 0 );
      track40.Sectors[SECTOR_BAM].Data.SetU8At( 1, 0xff );
      track40.Sectors[SECTOR_BAM].Free = false;

      /*
         04-8F: BAM entries for each track, in groups  of  four  bytes  per
                track, starting on track 1 (see below for more details)
         The BAM entries require a bit (no pun intended) more of a breakdown. Take
          the first entry at bytes $04-$07 ($12 $FF $F9 $17). The first byte ($12) is
          the number of free sectors on that track. Since we are looking at the track
          1 entry, this means it has 18 (decimal) free sectors. The next three  bytes
          represent the bitmap of which sectors are used/free. Since it is 3 bytes (8
          bits/byte) we have 24 bits of storage. Remember that at  most,  each  track
          only has 21 sectors, so there are a few unused bits.
       */
      Track trackBAM = Tracks[TRACK_BAM - 1];
      Sector sectorBAM = trackBAM.Sectors[SECTOR_BAM];
      Sector sectorBAM2 = trackBAM.Sectors[SECTOR_BAM + 1];
      int sectorOffset = 0;

      sectorBAM.Free = false;
      sectorBAM2.Free = false;

      // mark first directory sector as used
      track40.Sectors[SECTOR_DIRECTORY].Free = false;

      // point to second BAM sector
      sectorBAM.Data.SetU8At( 0, (byte)TRACK_BAM );
      sectorBAM.Data.SetU8At( 1, (byte)( SECTOR_BAM + 1 ) );
      sectorBAM.Data.SetU8At( 2, 0x44 );   // version
      sectorBAM.Data.SetU8At( 3, 0xbb );   // one complement from version
      sectorBAM.Data.SetU8At( 6, 0xc0 );   // IO byte
      sectorBAM2.Data.SetU8At( 0, 0 );
      sectorBAM2.Data.SetU8At( 1, 0xff );
      sectorBAM2.Data.SetU8At( 2, 0x44 );   // version
      sectorBAM2.Data.SetU8At( 3, 0xbb );   // one complement from version
      sectorBAM2.Data.SetU8At( 6, 0xc0 );   // IO byte
      for ( int i = 0; i < Tracks.Count; ++i )
      {
        if ( i == 40 )
        {
          sectorBAM = sectorBAM2;
          sectorOffset = -40;
        }
        sectorBAM.Data.SetU8At( 16 + 6 * ( i + sectorOffset ), (byte)Tracks[i].FreeSectors );

        sectorBAM.Data.SetU8At( 16 + 6 * ( i + sectorOffset ) + 1, 0 );
        sectorBAM.Data.SetU8At( 16 + 6 * ( i + sectorOffset ) + 2, 0 );
        sectorBAM.Data.SetU8At( 16 + 6 * ( i + sectorOffset ) + 3, 0 );
        sectorBAM.Data.SetU8At( 16 + 6 * ( i + sectorOffset ) + 4, 0 );
        sectorBAM.Data.SetU8At( 16 + 6 * ( i + sectorOffset ) + 5, 0 );

        for ( int j = 0; j < Tracks[i].Sectors.Count; ++j )
        {
          if ( Tracks[i].Sectors[j].Free )
          {
            byte oldValue = sectorBAM.Data.ByteAt( 16 + 6 * ( i + sectorOffset ) + 1 + j / 8 );
            oldValue |= (byte)( 1 << ( j % 8 ) );
            sectorBAM.Data.SetU8At( 16 + 6 * ( i + sectorOffset ) + 1 + j / 8, oldValue );
          }
        }
      }

      // disk name (padded with 0xa0)
      SetDiskName( "EMPTY DISK" );

      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 0x14, 0xa0 );
      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 0x15, 0xa0 );

      SetDiskID( "1a" );

      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 0x18, 0xa0 );

      // DOS type
      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 0x19, (byte)'3' );
      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 0x1a, (byte)'D' );
      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 0x1b, 0xa0 );
      track40.Sectors[SECTOR_HEADER].Data.SetU8At( 0x1c, 0xa0 );

      // track pointer of first directory sector
      track40.Sectors[SECTOR_DIRECTORY].Data.SetU8At( 0, 0 );
      track40.Sectors[SECTOR_DIRECTORY].Data.SetU8At( 1, 0xff );
    }



    public override bool Load( string Filename )
    {
      _LastError = "";
      GR.Memory.ByteBuffer diskData = GR.IO.File.ReadAllBytes( Filename );
      if ( diskData == null )
      {
        _LastError = "Could not open/read file";
        return false;
      }
      if ( ( diskData.Length != 822400 )      // with error bytes
      &&   ( diskData.Length != 819200 ) )
      {
        _LastError = "disk image size is not equal 819200 or 822400 bytes";
        return false;
      }
      CreateEmptyMedia();

      int     dataPos = 0;
      for ( int i = 0; i < Tracks.Count; ++i )
      {
        for ( int j = 0; j < Tracks[i].Sectors.Count; ++j )
        {
          diskData.CopyTo( Tracks[i].Sectors[j].Data, dataPos, 256 );
          dataPos += 256;
        }
      }

      for ( int i = 0; i < Tracks.Count; ++i )
      {
        for ( int j = 0; j < Tracks[i].Sectors.Count; ++j )
        {
          Tracks[i].Sectors[j].Free = !IsSectorMarkedAsUsedInBAM( i + 1, j );
        }
      }

      if ( diskData.Length == 822400 )
      {
        // error info appended
        for ( int i = 0; i < Tracks.Count; ++i )
        {
          for ( int j = 0; j < Tracks[i].Sectors.Count; ++j )
          {
            Tracks[i].Sectors[j].SectorErrorCode = diskData.ByteAt( dataPos );
            ++dataPos;
          }
        }
      }
      return true;
    }



    public override bool Save( string Filename )
    {
      GR.Memory.ByteBuffer data = Compile();
      return GR.IO.File.WriteAllBytes( Filename, data );
    }



    bool LocateFile( GR.Memory.ByteBuffer Filename, out Location FileLocation, out Types.FileInfo FileInfo )
    {
      _LastError = "";
      FileLocation = null;
      FileInfo = null;

      Location    curLoc = new Location( TRACK_DIRECTORY, SECTOR_DIRECTORY );

      bool endFound = false;

      while ( !endFound )
      {
        Sector sec = Tracks[curLoc.Track - 1].Sectors[curLoc.Sector];

        for ( int i = 0; i < 8; ++i )
        {
          int fileTrack = sec.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 3 );
          int fileSector = sec.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 4 );
          if ( fileTrack != 0 )
          {
            // valid entry?
            if ( sec.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 2 ) != (byte)C64Studio.Types.FileType.SCRATCHED )
            {
              GR.Memory.ByteBuffer filename = sec.Data.SubBuffer( BYTES_PER_DIR_ENTRY * i + 5, 16 );
              if ( Filename.Compare( filename ) == 0 )
              {
                FileLocation = new Location( fileTrack, fileSector );

                FileInfo              = new C64Studio.Types.FileInfo();
                FileInfo.Filename     = new GR.Memory.ByteBuffer( filename );
                FileInfo.StartSector  = fileSector;
                FileInfo.StartTrack   = fileTrack;
                FileInfo.Type         = (C64Studio.Types.FileType)sec.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 2 );
                FileInfo.Blocks       = 0;
                return true;
              }
            }
          }
        }

        curLoc = sec.NextLocation;
        if ( curLoc == null )
        {
          // track = 0 marks last directory entry
          break;
        }
      }
      _LastError = "Could not locate directory entry for file";
      return false;
    }



    public override Types.FileInfo LoadFile( GR.Memory.ByteBuffer Filename )
    {
      _LastError = "";

      Location fileLocation;
      Types.FileInfo    fileInfo;

      if ( !LocateFile( Filename, out fileLocation, out fileInfo ) )
      {
        _LastError = "File not found";
        return null;
      }

      GR.Memory.ByteBuffer result = new GR.Memory.ByteBuffer();

      bool endFound = false;

      while ( !endFound )
      {
        Sector  sec       = Tracks[fileLocation.Track - 1].Sectors[fileLocation.Sector];
        fileLocation      = sec.NextLocation;
        if ( fileLocation == null )
        {
          result.Append( sec.Data.SubBuffer( 2, sec.Data.ByteAt( 1 ) - 1 ) );
          endFound = true;
          break;
        }
        result.Append( sec.Data.SubBuffer( 2 ) );
      }
      fileInfo.Data = result;
      return fileInfo;
    }



    public override bool IsSectorAllocated( int Track, int Sector )
    {
      _LastError = "";

      if ( ( Track < 1 )
      ||   ( Track > Tracks.Count ) )
      {
        _LastError = "Track index out of bounds";
        return false;
      }
      Track track = Tracks[Track - 1];

      if ( ( Sector < 0 )
      ||   ( Sector >= track.Sectors.Count ) )
      {
        _LastError = "Sector index out of bounds";
        return false;
      }
      Sector bam = Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM];
      int     trackOffset = -1;
      if ( Track >= 41 )
      {
        bam = Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM + 1];
        trackOffset = -41;
      }

      byte mask = (byte)( 1 << ( Sector & 7 ) );
      if ( ( bam.Data.ByteAt( 16 + ( Track + trackOffset ) * 6 + Sector / 8 + 1 ) & mask ) != 0 )
      {
        return false;
      }
      return true;
    }



    private void AllocSector( int Track, int Sector )
    {
      _LastError = "";
      if ( ( Track < 1 )
      ||   ( Track > Tracks.Count ) )
      {
        _LastError = "track index out of bounds";
        return;
      }
      Track track = Tracks[Track - 1];

      if ( ( Sector < 0 )
      ||   ( Sector >= track.Sectors.Count ) )
      {
        _LastError = "sector index out of bounds";
        return;
      }
      if ( IsSectorAllocated( Track, Sector ) )
      {
        return;
      }
      Sector  bam = Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM];
      int     trackOffset = -1;
      if ( Track >= 41 )
      {
        bam = Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM + 1];
        trackOffset = -41;
      }

      // adjust free sectors
      bam.Data.SetU8At( 16 + ( Track + trackOffset ) * 6, (byte)( bam.Data.ByteAt( 16 + ( Track + trackOffset ) * 6 ) - 1 ) );

      // mask out sector
      byte mask = (byte)( 1 << ( Sector & 7 ) );
      bam.Data.SetU8At( 16 + ( Track + trackOffset ) * 6 + Sector / 8 + 1, (byte)( bam.Data.ByteAt( 16 + ( Track + trackOffset ) * 6 + Sector / 8 + 1 ) & ~mask ) );

      Tracks[Track - 1].Sectors[Sector].Free = false;
    }



    private void FreeSector( int Track, int Sector )
    {
      _LastError = "";
      if ( ( Track < 1 )
      ||   ( Track > Tracks.Count ) )
      {
        _LastError = "track index out of bounds";
        return;
      }
      Track track = Tracks[Track - 1];

      if ( ( Sector < 0 )
      ||   ( Sector >= track.Sectors.Count ) )
      {
        _LastError = "sector index out of bounds";
        return;
      }
      if ( !IsSectorAllocated( Track, Sector ) )
      {
        return;
      }
      Sector bam = Tracks[17].Sectors[0];

      // adjust free sectors
      bam.Data.SetU8At( Track * 4, (byte)( bam.Data.ByteAt( Track * 4 ) + 1 ) );

      // mask in sector
      byte mask = (byte)( 1 << ( Sector & 7 ) );
      bam.Data.SetU8At( Track * 4 + Sector / 8 + 1, (byte)( bam.Data.ByteAt( Track * 4 + Sector / 8 + 1 ) | mask ) );

      Tracks[Track - 1].Sectors[Sector].Free = true;
    }



    bool AddDirectoryEntry( GR.Memory.ByteBuffer Filename, int StartTrack, int StartSector, int SectorsWritten, C64Studio.Types.FileType Type )
    {
      _LastError = "";
      Track   dirTrack = Tracks[TRACK_DIRECTORY - 1];
      byte    dirTrackIndex = (byte)TRACK_DIRECTORY;

      for ( int sector = SECTOR_DIRECTORY; sector < dirTrack.Sectors.Count; ++sector )
      {
        Sector sect = dirTrack.Sectors[sector];
        for ( int i = 0; i < 8; ++i )
        {
          if ( sect.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 2 ) == 0 )
          {
            // scratched (empty) entry

            // default set PRG
            sect.Data.SetU8At( BYTES_PER_DIR_ENTRY * i + 2, (byte)Type );
            sect.Data.SetU8At( BYTES_PER_DIR_ENTRY * i + 3, (byte)StartTrack );
            sect.Data.SetU8At( BYTES_PER_DIR_ENTRY * i + 4, (byte)StartSector );

            for ( int j = 0; j < 16; ++j )
            {
              sect.Data.SetU8At( BYTES_PER_DIR_ENTRY * i + 5 + j, Filename.ByteAt( j ) );
            }
            sect.Data.SetU16At( BYTES_PER_DIR_ENTRY * i + 30, (UInt16)SectorsWritten );
            return true;
          }
        }
        // do we need to alloc next dir sector?
        if ( sector + 1 == dirTrack.Sectors.Count )
        {
          // disk full!! (or continue on next track?)
          _LastError = "disk is full";
          return false;
        }
        if ( sect.Data.ByteAt( 0 ) == 0 )
        {
          // current sector was last dir sector
          sect.Data.SetU8At( 0, dirTrackIndex );
          sect.Data.SetU8At( 1, (byte)( sector + 1 ) );
          AllocSector( dirTrackIndex, sector + 1 );

          dirTrack.Sectors[sector + 1].Data.SetU8At( 0, 0 );
          dirTrack.Sectors[sector + 1].Data.SetU8At( 1, 0xff );
        }
      }
      _LastError = "disk is full";
      return false;
    }



    public override bool DeleteFile( GR.Memory.ByteBuffer Filename )
    {
      _LastError = "";
      int   curTrack = TRACK_DIRECTORY;
      int   curSector = SECTOR_DIRECTORY;

      while ( true )
      {
        Track dirTrack = Tracks[curTrack - 1];

        Sector sect = dirTrack.Sectors[curSector];
        for ( int i = 0; i < 8; ++i )
        {
          if ( sect.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 2 ) != 0 )
          {
            // non empty
            bool  filenameMatches = true;
            for ( int j = 0; j < 16; ++j )
            {
              if ( Filename.ByteAt( j ) != sect.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 5 + j ) )
              {
                filenameMatches = false;
                break;
              }
            }

            if ( filenameMatches )
            {
              // scratch file
              sect.Data.SetU8At( BYTES_PER_DIR_ENTRY * i + 2, 0 );

              int startTrack  = sect.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 3 );
              int startSector = sect.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 4 );

              while ( startTrack != 0 )
              {
                Track fileTrack = Tracks[startTrack - 1];
                Sector fileSector = fileTrack.Sectors[startSector];

                FreeSector( startTrack, startSector );

                startTrack = fileSector.Data.ByteAt( 0 );
                startSector = fileSector.Data.ByteAt( 1 );
              }
              return true;
            }
          }
        }
        // find next dir sector
        if ( sect.Data.ByteAt( 0 ) == 0 )
        {
          // was last dir sector
          _LastError = "file not found";
          return false;
        }
        curTrack  = sect.Data.ByteAt( 0 );
        curSector = sect.Data.ByteAt( 1 );
      }
    }



    public override bool RenameFile( GR.Memory.ByteBuffer Filename, GR.Memory.ByteBuffer NewFilename )
    {
      _LastError = "";
      int curTrack = TRACK_DIRECTORY;
      int curSector = SECTOR_DIRECTORY;
      while ( true )
      {
        Track dirTrack = Tracks[curTrack - 1];

        Sector sect = dirTrack.Sectors[curSector];
        for ( int i = 0; i < 8; ++i )
        {
          if ( sect.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 2 ) != 0 )
          {
            // non empty
            bool filenameMatches = true;
            for ( int j = 0; j < 16; ++j )
            {
              if ( Filename.ByteAt( j ) != sect.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 5 + j ) )
              {
                filenameMatches = false;
                break;
              }
            }

            if ( filenameMatches )
            {
              for ( int j = 0; j < 16; ++j )
              {
                sect.Data.SetU8At( BYTES_PER_DIR_ENTRY * i + 5 + j, NewFilename.ByteAt( j ) );
              }
              return true;
            }
          }
        }
        // find next dir sector
        if ( sect.Data.ByteAt( 0 ) == 0 )
        {
          // was last dir sector
          _LastError = "file not found";
          return false;
        }
        curTrack = sect.Data.ByteAt( 0 );
        curSector = sect.Data.ByteAt( 1 );
      }
    }



    public override bool WriteFile( GR.Memory.ByteBuffer Filename, GR.Memory.ByteBuffer Content, C64Studio.Types.FileType Type )
    {
      _LastError = "";
      GR.Memory.ByteBuffer    dataToWrite = new GR.Memory.ByteBuffer( Content );
      if ( dataToWrite.Length > FreeBytes() )
      {
        _LastError = "file too large";
        return false;
      }

      Sector bam = Tracks[TRACK_BAM - 1].Sectors[SECTOR_BAM];

      int trackIndex = 1;
      int prevSector = 0;
      int interleave = 10;
      int bytesToWrite = (int)dataToWrite.Length;
      int writeOffset = 0;
      Sector previousSector = null;
      int searchSector = -1; 

      int startSector = -1;
      int startTrack = -1;
      int sectorsWritten = 0;

      write_next_sector:;
      trackIndex = 1;
      foreach ( Track track in Tracks )
      {
        if ( trackIndex == TRACK_DIRECTORY )
        {
          // directory track
          ++trackIndex;
          continue;
        }
        if ( track.FreeSectors == 0 )
        {
          ++trackIndex;
          continue;
        }

        int     sectorsPerTrack = track.Sectors.Count;
        searchSector = ( prevSector + interleave ) % sectorsPerTrack;

        while ( true )
        {
          if ( track.Sectors[searchSector].Free )
          {
            AllocSector( trackIndex, searchSector );
            if ( previousSector != null )
            {
              previousSector.Data.SetU8At( 0, (byte)trackIndex );
              previousSector.Data.SetU8At( 1, (byte)searchSector );
            }
            else
            {
              // first sector, add directory entry
              startSector = searchSector;
              startTrack = trackIndex;
            }
            previousSector = track.Sectors[searchSector];
            if ( bytesToWrite > 254 )
            {
              //Debug.Log( "Write to T/S " + trackIndex + "," + searchSector );
              dataToWrite.CopyTo( previousSector.Data, writeOffset, 254, 2 );
              previousSector.Free = false;
              writeOffset += 254;
              bytesToWrite -= 254;
              ++sectorsWritten;
              prevSector = searchSector;
              goto write_next_sector;
            }
            // last sector
            //Debug.Log( "Write to T/S " + trackIndex + "," + searchSector );
            previousSector.Free = false;
            previousSector.Data.SetU8At( 0, 0 );
            previousSector.Data.SetU8At( 1, (byte)( 1 + bytesToWrite ) );
            dataToWrite.CopyTo( previousSector.Data, writeOffset, bytesToWrite, 2 );
            writeOffset += bytesToWrite;
            bytesToWrite = 0;
            ++sectorsWritten;

            AddDirectoryEntry( Filename, startTrack, startSector, sectorsWritten, Type );
            return true;
          }
          else
          {
            ++searchSector;
            if ( searchSector >= sectorsPerTrack )
            {
              searchSector = 0;
            }
          }
        }
      }
      _LastError = "disk is full";
      return false;
    }



    public override GR.Memory.ByteBuffer Compile()
    {
      _LastError = "";
      GR.Memory.ByteBuffer result = new GR.Memory.ByteBuffer();

      foreach ( Track track in Tracks )
      {
        foreach ( Sector sector in track.Sectors )
        {
          result.Append( sector.Data );
        }
      }
      return result;
    }



    public override int FreeSlots
    {
      get
      {
        return FreeBlocks;
      }
    }



    public override int Slots
    {
      get
      {
        return Blocks;
      }
    }



    private void FollowChain( int Track,
                              int Sector,
                              GR.Collections.Map<GR.Generic.Tupel<int, int>, GR.Memory.ByteBuffer> UsedSectors,
                              GR.Memory.ByteBuffer Filename,
                              List<string> Errors )
    {
      int fileTrack = Track;
      int fileSector = Sector;

      while ( fileTrack != 0 )
      {
        GR.Generic.Tupel<int,int>     location = new GR.Generic.Tupel<int,int>( fileTrack, fileSector );

        if ( ( UsedSectors[location].Length > 0 )
        &&   ( UsedSectors[location] != Filename ) )
        {
          Errors.Add( "Sector " + location.first + ", Track " + location.second + " is referenced by more than one file" );
        }

        int newTrack = Tracks[fileTrack - 1].Sectors[fileSector].Data.ByteAt( 0 );
        int newSector = Tracks[fileTrack - 1].Sectors[fileSector].Data.ByteAt( 1 );

        if ( newTrack == 0 )
        {
          return;
        }

        if ( ( newTrack < 1 )
        ||   ( newTrack > Tracks.Count ) )
        {
          Errors.Add( "Reference to invalid track " + newTrack + " encountered in track " + fileTrack + ", Sector " + fileSector );
          return;
        }
        fileTrack = newTrack;
        fileSector = newSector;
      }
    }



    private Sector FindPreviousDirSector( int Track, int Sector )
    {
      int   curTrack = Track;

      foreach ( Sector sec in Tracks[curTrack - 1].Sectors )
      {
        if ( ( sec.Data.ByteAt( 0 ) == Track )
        &&   ( sec.Data.ByteAt( 1 ) == Sector ) )
        {
          // this sector points at me
          return sec;
        }
      }
      return null;
    }



    private Sector FindNextDirSector( int Track, int Sector )
    {
      int nextTrack = Tracks[Track - 1].Sectors[Sector].Data.ByteAt( 0 );
      if ( nextTrack == 0 )
      {
        return null;
      }
      int nextSector = Tracks[Track - 1].Sectors[Sector].Data.ByteAt( 1 );
      return Tracks[nextTrack - 1].Sectors[nextSector];
    }



    private DirEntryLocation LocateDirEntry( int Track, int Sector )
    {
      Location  curLoc = new Location( TRACK_DIRECTORY, SECTOR_DIRECTORY );

      while ( true )
      {
        Sector sec = Tracks[curLoc.Track - 1].Sectors[curLoc.Sector];

        for ( int i = 0; i < 8; ++i )
        {
          int fileTrack  = sec.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 3 );
          int fileSector = sec.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 4 );
          if ( sec.Data.ByteAt( BYTES_PER_DIR_ENTRY * i + 2 ) != (byte)C64Studio.Types.FileType.SCRATCHED )
          {
            // valid entry?
            if ( ( fileTrack == Track )
            &&   ( fileSector == Sector ) )
            {
              return new DirEntryLocation( curLoc.Track, curLoc.Sector, i );
            }
          }
        }
        curLoc = sec.NextLocation;
        if ( curLoc == null )
        {
          // track = 0 marks last directory entry
          break;
        }
      }
      return null;
    }



    private bool FindPreviousDirEntry( DirEntryLocation DirEntry, out DirEntryLocation ResultDirEntry )
    {
      ResultDirEntry = null;

      if ( DirEntry.DirEntry > 0 )
      {
        ResultDirEntry = new DirEntryLocation( DirEntry.Track, DirEntry.Sector, DirEntry.DirEntry - 1 );
        return true;
      }
      int curTrack = DirEntry.Track;
      foreach ( Sector sec in Tracks[curTrack - 1].Sectors )
      {
        if ( sec.NextLocation == null )
        {
          continue;
        }
        if ( ( sec.NextLocation.Track == DirEntry.Track )
        &&   ( sec.NextLocation.Sector == DirEntry.Sector ) )
        {
          // this sector points at me
          ResultDirEntry = new DirEntryLocation( sec.TrackNo, sec.SectorNo, 7 );
          return true;
        }
      }
      return false;
    }



    private bool FindNextDirEntry( DirEntryLocation DirEntry, out DirEntryLocation ResultDirEntry )
    {
      ResultDirEntry = null;

      if ( DirEntry.DirEntry < 7 )
      {
        ResultDirEntry = new DirEntryLocation( DirEntry.Track, DirEntry.Sector, DirEntry.DirEntry + 1 );
        return true;
      }
      Location    nextSector = Tracks[DirEntry.Track - 1].Sectors[DirEntry.Sector].NextLocation;
      if ( nextSector == null )
      {
        return false;
      }
      Sector sec = Tracks[nextSector.Track - 1].Sectors[nextSector.Sector];
      ResultDirEntry = new DirEntryLocation( sec.TrackNo, sec.SectorNo, 0 );
      return true;
    }



    public override string FileFilter
    {
      get
      {
        return "Disk Files|*.D81|" + base.FileFilter;
      }
    }



    public override GR.Memory.ByteBuffer Title
    {
      get
      {
        GR.Memory.ByteBuffer    title = new GR.Memory.ByteBuffer();

        title.Append( Util.ToPETSCII( "0 \"" ) );
        title.Append( DiskName );
        title.Append( Util.ToPETSCII( "\" " ) );
        title.AppendU16NetworkOrder( DiskID );
        return title;
      }
    }



    public override string LastError
    {
      get 
      {
        return _LastError;
      }
    }



    public override void Validate()
    {
      var files = Files();

      GR.Collections.Set<GR.Generic.Tupel<int,int>>    usedTracksAndSectors = new GR.Collections.Set<GR.Generic.Tupel<int, int>>();

      usedTracksAndSectors.Add( new GR.Generic.Tupel<int, int>( TRACK_HEADER, SECTOR_HEADER ) );
      usedTracksAndSectors.Add( new GR.Generic.Tupel<int, int>( TRACK_BAM, SECTOR_BAM ) );

      int   curTrack = TRACK_DIRECTORY;
      int   curSector = SECTOR_DIRECTORY;
      usedTracksAndSectors.Add( new GR.Generic.Tupel<int, int>( curTrack, curSector ) );
      while ( true )
      {
        Sector sec = Tracks[curTrack - 1].Sectors[curSector];

        curTrack = sec.Data.ByteAt( 0 );
        curSector = sec.Data.ByteAt( 1 );

        if ( curTrack == 0 )
        {
          // track = 0 marks last directory entry
          break;
        }
        usedTracksAndSectors.Add( new GR.Generic.Tupel<int, int>( curTrack, curSector ) );
      }

      foreach ( var file in files )
      {
        curTrack = file.StartTrack;
        curSector = file.StartSector;

        while ( true )
        {
          Sector sec = Tracks[curTrack - 1].Sectors[curSector];

          usedTracksAndSectors.Add( new GR.Generic.Tupel<int, int>( curTrack, curSector ) );

          curTrack = sec.Data.ByteAt( 0 );
          curSector = sec.Data.ByteAt( 1 );

          if ( curTrack == 0 )
          {
            // track = 0 marks last directory entry
            break;
          }
        }
      }
      foreach ( var track in Tracks )
      {
        foreach ( var sector in track.Sectors )
        {
          if ( ( !sector.Free )
          && ( !usedTracksAndSectors.ContainsValue( new GR.Generic.Tupel<int, int>( track.TrackNo, sector.SectorNo ) ) ) )
          {
            sector.Free = true;
          }
        }
      }

    }

  }
}
