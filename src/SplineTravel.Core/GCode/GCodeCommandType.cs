namespace SplineTravel.Core.GCode;

/// <summary>
/// G-code command type. VB6 encoding: (number * 0x100 + letter), e.g. G1 = 0x147.
/// </summary>
public enum GCodeCommandType
{
    Empty = 0,
    G0 = 0x047,     // quick move
    G1 = 0x147,     // controlled move
    G4 = 0x447,     // dwell
    G21 = 0x1547,   // set unit mm
    M82 = 0x524D,   // absolute E
    M83 = 0x534D,   // relative E
    G90 = 0x5A47,   // absolute pos
    G91 = 0x5B47,   // relative pos
    G92 = 0x5C47,   // override pos
}
