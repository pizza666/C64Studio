;to set the proper CPU
!cpu m65

;to include VIC constants
!source <c64.asm>

;to include Mega65 constants VIC3 and VIC4
!source <mega65.asm>


SCREEN_CHAR   = $0800

;for 40x25 the lower 1000 bytes of the color RAM are mapped here
SCREEN_COLOR  = $d800


* = $2001

          ;this must follow after !cpu m65, because it adds a BANK to the BASIC upstart
          !basic

          sei

          +EnableVIC4Registers

          ;Turn off CIA interrupts
          lda #$7f
          sta CIA1.IRQ_CONTROL
          sta CIA2.NMI_CONTROL

          ;Turn off raster interrupts, used by C65 rom
          lda #$00
          sta VIC.IRQ_MASK

          cli

          ;set 40x25 mode
          lda #$80
          trb VIC3.VICDIS

          lda #0
          sta VIC.BACKGROUND_COLOR

          ldx #0
-
          lda TEXT_SCREEN_DATA,x
          sta SCREEN_CHAR,x
          lda TEXT_SCREEN_DATA + 1 * 250,x
          sta SCREEN_CHAR + 1 * 250,x
          lda TEXT_SCREEN_DATA + 2 * 250,x
          sta SCREEN_CHAR + 2 * 250,x
          lda TEXT_SCREEN_DATA + 3 * 250,x
          sta SCREEN_CHAR + 3 * 250,x

          lda TEXT_SCREEN_DATA + 1000,x
          sta SCREEN_COLOR,x
          lda TEXT_SCREEN_DATA + 1000 + 1 * 250,x
          sta SCREEN_COLOR + 1 * 250,x
          lda TEXT_SCREEN_DATA + 1000 + 2 * 250,x
          sta SCREEN_COLOR + 2 * 250,x
          lda TEXT_SCREEN_DATA + 1000 + 3 * 250,x
          sta SCREEN_COLOR + 3 * 250,x

          inx
          cpx #250
          bne -

          ;endless loop
          jmp *



TEXT_SCREEN_DATA
          ;contains 40x25 character bytes, followed by 40x25 color bytes
          !media "Text Screen.charscreen",CHARCOLOR

