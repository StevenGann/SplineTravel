using SplineTravel.Core.Precision;

namespace SplineTravel.Core.Geometry;

/// <summary>
/// Retraction as a function of time (parabolic accel/decel). Ported from VB6 clsRetractCurve.
/// getValue(t) = cumulative retraction at parameter t in [0,1].
/// </summary>
public sealed class RetractProfile
{
    private double _moveTime = 1;
    private double _retractLenSetting = 1;
    private double _retractA = 1000;
    private bool _bRetract = true;
    private bool _bUnretract = true;
    private double _derivJerk = 1;
    private bool _invalidated = true;

    private double _retractLen;
    private double _dtAcc;
    private double _tStart1, _tMid1, _tEnd1;
    private double _tStart2, _tMid2, _tEnd2;

    public double MoveTime { get => _moveTime; set { _moveTime = value; _invalidated = true; } }
    public double RetractLen { get => _retractLenSetting; set { _retractLenSetting = value; _invalidated = true; } }
    public double RetractA { get => _retractA; set { _retractA = value; _invalidated = true; } }
    public bool BRetract { get => _bRetract; set { _bRetract = value; _invalidated = true; } }
    public bool BUnretract { get => _bUnretract; set { _bUnretract = value; _invalidated = true; } }
    public double DerivJerk { get => _derivJerk; set { _derivJerk = value; _invalidated = true; } }

    private void Recompute()
    {
        _dtAcc = Math.Sqrt(Math.Abs(2 * (_retractLenSetting / 2) / _retractA));
        if (_bRetract && _bUnretract)
        {
            if (_dtAcc * 4 > _moveTime)
            {
                _dtAcc = _moveTime / 4;
                _retractLen = _retractA * _dtAcc * _dtAcc;
            }
            else
                _retractLen = _retractLenSetting;
        }
        else if (_bRetract || _bUnretract)
        {
            if (_dtAcc * 2 > _moveTime)
            {
                _dtAcc = _moveTime / 2;
                _retractA = (_retractLenSetting / 2) / (_moveTime / 2) / (_moveTime / 2);
                _retractLen = _retractLenSetting;
            }
            else
                _retractLen = _retractLenSetting;
        }
        else
            _retractLen = 0;

        _tStart1 = 0;
        if (!_bRetract) _tStart1 -= 10 * _moveTime;
        _tMid1 = _tStart1 + _dtAcc;
        _tEnd1 = _tMid1 + _dtAcc;

        _tEnd2 = _moveTime;
        if (!_bUnretract) _tEnd2 += 10 * _moveTime;
        _tMid2 = _tEnd2 - _dtAcc;
        _tStart2 = _tMid2 - _dtAcc;
        _invalidated = false;
    }

    public double GetValue(double t)
    {
        if (_invalidated) Recompute();
        double time = t * _moveTime;
        if (time < _tStart1) return 0;
        if (time < _tMid1) return _retractA * (time - _tStart1) * (time - _tStart1) / 2;
        if (time < _tEnd1) return _bRetract ? _retractLen - _retractA * (time - _tEnd1) * (time - _tEnd1) / 2 : _retractLen;
        if (time < _tStart2) return _retractLen;
        if (time < _tMid2) return _retractLen - _retractA * (time - _tStart2) * (time - _tStart2) / 2;
        if (time < _tEnd2) return _retractA * (time - _tEnd2) * (time - _tEnd2) / 2;
        return 0;
    }

    public double GetDeriv(double t)
    {
        if (_invalidated) Recompute();
        double time = t * _moveTime;
        if (time < _tStart1) return 0;
        if (time < _tMid1) return _retractA * (time - _tStart1) * _moveTime;
        if (time < _tEnd1) return -_retractA * (time - _tEnd1) * _moveTime;
        if (time < _tStart2) return 0;
        if (time < _tMid2) return -_retractA * (time - _tStart2) * _moveTime;
        if (time < _tEnd2) return _retractA * (time - _tEnd2) * _moveTime;
        return 0;
    }

    public double GetDeriv2(double t)
    {
        if (_invalidated) Recompute();
        double time = (t + PrecisionSettings.RelConfusion) * _moveTime;
        if (time < _tStart1) return 0;
        if (time < _tMid1) return _retractA * _moveTime * _moveTime;
        if (time < _tEnd1) return -_retractA * _moveTime * _moveTime;
        if (time < _tStart2) return 0;
        if (time < _tMid2) return -_retractA * _moveTime * _moveTime;
        if (time < _tEnd2) return _retractA * _moveTime * _moveTime;
        return 0;
    }

    private double GetNextBreakpoint(double prevT)
    {
        double ret = 1;
        double tBp = _tStart1 / _moveTime;
        if (prevT < tBp - PrecisionSettings.RelConfusion && ret > tBp + PrecisionSettings.RelConfusion)
            ret = tBp;
        tBp = _tStart2 / _moveTime;
        if (prevT < tBp - PrecisionSettings.RelConfusion && ret > tBp + PrecisionSettings.RelConfusion)
            ret = tBp;
        return ret;
    }

    public bool ShrinkInterval(double prevT, ref double curT)
    {
        if (_invalidated) Recompute();
        double tBreakpoint = GetNextBreakpoint(prevT);
        double tstep = 100;
        double acc = Math.Abs(GetDeriv2(prevT));
        if (acc > 1e-100) tstep = _derivJerk / acc;
        if (prevT + tstep > tBreakpoint) tstep = tBreakpoint - prevT;
        double tstepToEnd = tBreakpoint - prevT;
        if (tstep < tstepToEnd - PrecisionSettings.RelConfusion && tstepToEnd - tstep < tstepToEnd * 0.25)
            tstep = tstepToEnd * 0.3;
        if (curT > prevT + tstep + PrecisionSettings.RelConfusion)
        {
            curT = prevT + tstep;
            return true;
        }
        return false;
    }
}
