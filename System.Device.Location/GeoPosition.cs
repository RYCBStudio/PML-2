// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*=============================================================================
**
** Class: GeoPosition
**
** Purpose: Represents a GeoPosition object
**
=============================================================================*/

namespace System.Device.Location;

public class GeoPosition<T>
{
    #region Constructors

    public GeoPosition() :
        this(DateTimeOffset.MinValue, default)
    {
    }

    public GeoPosition(DateTimeOffset timestamp, T position)
    {
        Timestamp = timestamp;
        Location = position;
    }

    #endregion

    #region Properties

    public T Location
    {
        get;

        set;
    }

    public DateTimeOffset Timestamp
    {
        get;
        set;
    } = DateTimeOffset.MinValue;

    #endregion
}