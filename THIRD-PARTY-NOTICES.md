# Third-party notices

This package is original work, with one exception, noted here.

## siriwave

The waveform state of `IVARenderer` (the `wave_*` parameters and the curve maths in the
"ios9 Siri wave" region of `Runtime/IVARenderer.cs`) is a C# port of the `ios9-curve`
implementation from **siriwave** by Flavio De Stefano (kopiro).

- Source: https://github.com/kopiro/siriwave
- License: MIT

```
MIT License

Copyright (c) Flavio De Stefano

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Everything else in this package — the face, eye, mouth and ear geometry, the parameter
system, the colour model, the glow, the auto-fit, the slider panel and the editor tooling
— is the author's own work.
