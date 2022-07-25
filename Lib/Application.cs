﻿/*
 * Copyright (c) 2022 Leonardo Pessoa
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Diagnostics;

using Lmpessoa.Mainframe.Fields;

namespace Lmpessoa.Mainframe;

/// <summary>
/// 
/// </summary>
public sealed partial class Application {

   /// <summary>
   /// 
   /// </summary>
   public static bool IsRunning
      => _current is not null && _current._maps.Any();

   /// <summary>
   /// 
   /// </summary>
   /// <param name="code"></param>
   public static void Exit(int code = -1) {
      if (_current is not null) {
         _current.ReturnCode = code;
         _current.Stop();
      }
   }

   /// <summary>
   /// 
   /// </summary>
   public static void Logout() {
      if (_current is not null) {
         while (_current._maps.Any()) {
            if (_current._maps.Peek() is LoginMap loginMap) {
               if (loginMap.Activate() is (int cleft, int ctop)) {
                  _current.Console.SetCursorPosition(cleft, ctop);
               }
               return;
            }
            _current._maps.Pop();
         }
      }
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="map"></param>
   /// <returns></returns>
   public static int Run(Map map)
      => new Application(map).Run();

   /// <summary>
   /// 
   /// </summary>
   /// <param name="initialMap"></param>
   public Application(Map initialMap) : this(initialMap, new SystemConsole()) { }

   /// <summary>
   /// 
   /// </summary>
   public int ReturnCode { get; private set; } = 0;

   /// <summary>
   /// 
   /// </summary>
   public void EnforceWindowSize() {
      AssertAppIsNotRunning();
      _enforceSize = true;
   }

   /// <summary>
   /// 
   /// </summary>
   public void PreserveGivenFieldValues() {
      AssertAppIsNotRunning();
      _preserveValues = PreserveValuesLevel.Values;
   }

   public void PreserveGivenFieldValuesAndDisplay() {
      AssertAppIsNotRunning();
      _preserveValues = PreserveValuesLevel.Display;
   }

   /// <summary>
   /// 
   /// </summary>
   /// <returns></returns>
   public int Run() {
      try {
         Start();
         while (IsRunning) {
            LoopStep();
         }
      } finally {
         Stop();
      }
      return ReturnCode;
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="left"></param>
   /// <param name="top"></param>
   public void SetDefaultPopupPosition(int left, int top) {
      AssertAppIsNotRunning();
      _popupPos = (Math.Max(left, -1), Math.Max(top, -1));
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="minutes"></param>
   public void SetMaxIdleTime(uint minutes)
      => SetMaxIdleTime(TimeSpan.FromMinutes(minutes));

   /// <summary>
   /// 
   /// </summary>
   /// <param name="span"></param>
   public void SetMaxIdleTime(TimeSpan span) {
      AssertAppIsNotRunning();
      if (span.Minutes < 1) {
         throw new ArgumentOutOfRangeException(nameof(span));
      }
      MaxIdleTime = span;
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="width"></param>
   /// <param name="height"></param>
   public void SetWindowSize(int width, int height) {
      AssertAppIsNotRunning();
      _consoleSize = (Math.Max(80, width), Math.Max(24, height));
   }

   /// <summary>
   /// 
   /// </summary>
   public void UseActiveFieldBackground() {
      AssertAppIsNotRunning();
      Console.UseActiveFieldBackground();
   }

   /// <summary>
   /// 
   /// </summary>
   public void UseBulletsInPasswordFields() {
      AssertAppIsNotRunning();
      _passwordChar = '\u25CF';
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="color"></param>
   public void UseCommandColorInBackground(ConsoleColor color) {
      AssertAppIsNotRunning();
      Console.UseHighlightColorInBackground(color);
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="color"></param>
   public void UseHighlightColorInForeground(ConsoleColor color) {
      AssertAppIsNotRunning();
      Console.UseHighlightColorInForeground(color);
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="border"></param>
   public void UseWindowBorder(WindowBorder border) {
      AssertAppIsNotRunning();
      _border = border;
   }



   internal static WindowBorder BorderStyle
      => _current?._border ?? WindowBorder.Ascii;

   internal static bool IsInsertMode
      => _current?._insertMode ?? true;

   internal static char PasswordChar
      => _current?._passwordChar ?? '*';

   internal static PreserveValuesLevel PreserveValues
      => _current?._preserveValues ?? PreserveValuesLevel.None;

   internal static void SetCursorPosition(int left, int top) {
      if (_current is not null) {
         _current.Console.SetCursorPosition(left, top);
      }
   }

   internal static string SimplifyKeyInfo(ConsoleKeyInfo info) {
      string result = "";
      result += info.Modifiers.HasFlag(ConsoleModifiers.Control) ? "Ctrl+" : "";
      result += info.Modifiers.HasFlag(ConsoleModifiers.Alt) ? "Alt+" : "";
      result += info.Modifiers.HasFlag(ConsoleModifiers.Shift) ? "Shift+" : "";
      result += info.Key.ToString();
      return result;
   }

   internal Application(Map initialMap, IConsole console) {
      _initialMap = initialMap ?? throw new ArgumentNullException(nameof(initialMap));
      Console = console ?? throw new ArgumentNullException(nameof(console));
   }

   internal Map? CurrentMap
      => _maps.Any() ? _maps.Peek() : null;

   internal TimeSpan? MaxIdleTime { get; set; } = null;

   internal static void Pop(Map map) {
      if (_current is not null && _current.CurrentMap == map) {
         if (map.Closing()) {
            map.Deactivate(_current.Console.CursorLeft, _current.Console.CursorTop);
            _current._maps.Pop();
            map.Closed();
            if (_current.CurrentMap is Map previous) {
               if (previous.Activate() is (int cleft, int ctop)) {
                  _current.Console.SetCursorPosition(cleft, ctop);
               }

            }
            _current._viewDirty = true;
         }
      }
   }

   internal static void Push(Map map) {
      int left = 0;
      int top = 0;
      if (_current is not null) {
         if (map.Width > _current._consoleSize.Width || map.Height > _current._consoleSize.Height) {
            throw new InvalidOperationException("Map is too large for the screen");
         }
         if (_current.CurrentMap is Map previous) {
            previous.Deactivate(_current.Console.CursorLeft, _current.Console.CursorTop);
         }
         if (map is LoginMap && _current._maps.Any(m => m is LoginMap)) {
            throw new InvalidOperationException("Application can have only one login map");
         }
         if (map.IsPopup) {
            if (_current._popupPos == (-1, -1)) {
               left = (_current._consoleSize.Width - map.Width) / 2;
               top = (_current._consoleSize.Height - map.Height) / 2;
            } else {
               (left, top) = _current._popupPos;
               if (_current._border != WindowBorder.None) {
                  left += 1;
                  top += 1;
               }
            }
         }
         map.Top = top;
         map.Left = left;
         map.Application = _current;
         map.Activate();
         map.FocusOnNextField();
         _current._maps.Push(map);
         map.Redraw(_current.Console, true);
         if (map.Fields.Any(f => f.IsFocusable) && map.CurrentField is FieldBase field) {
            _current.Console.SetCursorPosition(map.Left + field.Left, map.Top + field.Top);
         }
      }
   }

   internal void LoopStep() {
      if (MaxIdleTime is not null && (DateTime.Now - _inactiveSince) > MaxIdleTime) {
         Logout();
         if (!_maps.Any()) {
            Exit(-21);
            throw new TimeoutException();
         }
      }
      if (CurrentMap is Map map) {
         HandleIfKeyPressed();
         if (_viewDirty || map.Fields.Any(f => f.IsDirty)) {
            int cleft = Console.CursorLeft;
            int ctop = Console.CursorTop;
            Console.CursorVisible = false;
            if (_viewDirty) {
               Map? m2 = _maps.LastOrDefault(m => !m.IsPopup);
               foreach (Map m in _maps.SkipWhile(m => m != m2)) {
                  m.Redraw(Console, true);
               }
            } else {
               map.Redraw(Console, false);
            }
            _viewDirty = false;
            Console.CursorLeft = cleft;
            Console.CursorTop = ctop;
            if (CurrentMap?.CurrentField is FieldBase field && field is InputFieldBase) {
               Console.CursorVisible = true;
            }
         }
      }
   }

   internal void Start() {
      if (IsRunning) {
         throw new InvalidOperationException();
      }
      ReturnCode = 0;
      _current = this;
      _initCtrlC = Console.TreatControlCAsInput;
      _initCurSize = Console.CursorSize;
      _initBuffSize = (Console.BufferWidth, Console.BufferHeight);
      _initWinSize = (Console.WindowWidth, Console.WindowHeight);
      if (_enforceSize || _consoleSize.Width > Console.WindowWidth || _consoleSize.Height > Console.WindowHeight) {
         Console.SetWindowSize(_consoleSize.Width, _consoleSize.Height);
         Console.SetBufferSize(_consoleSize.Width, _consoleSize.Height);
      }
      _needsRestore = true;
      Console.TreatControlCAsInput = true;
      _inactiveSince = DateTime.Now;
      _initialMap.Show();
   }

   internal void Stop() {
      foreach (Map map in _maps) {
         map.Application = null;
      }
      _maps.Clear();
      if (_needsRestore) {
         Console.Clear();
         if (_enforceSize || _initWinSize.Width < _consoleSize.Width || _initWinSize.Height < _consoleSize.Height) {
            Console.SetWindowSize(_initWinSize.Width, _initWinSize.Height);
            Console.SetBufferSize(_initBuffSize.Width, _initBuffSize.Height);
         }
         Console.CursorSize = _initCurSize;
         Console.CursorVisible = true;
         Console.TreatControlCAsInput = _initCtrlC;
      }
      _needsRestore = false;
      _current = null;
   }

   private static Application? _current = null;

   private readonly Map _initialMap;
   private readonly Stack<Map> _maps = new();

   private PreserveValuesLevel _preserveValues = PreserveValuesLevel.None;
   private (int Width, int Height) _consoleSize = (80, 24);
   private (int Left, int Top) _popupPos = (-1, -1);
   private WindowBorder _border = WindowBorder.Ascii;
   private (int Width, int Height) _initBuffSize;
   private (int Width, int Height) _initWinSize;
   private bool _needsRestore = false;
   private bool _enforceSize = false;
   private char _passwordChar = '*';
   private bool _insertMode = true;
   private bool _viewDirty = false;
   private DateTime _inactiveSince;
   private int _initCurSize;
   private bool _initCtrlC;

   private IConsole Console { get; }

   [StackTraceHidden]
   private void AssertAppIsNotRunning() {
      if (_current == this && IsRunning) {
         throw new InvalidOperationException();
      }
   }

   private void HandleIfKeyPressed() {
      if (Console.KeyAvailable && CurrentMap is Map map) {
         ConsoleKeyInfo key = Console.ReadKey();
         switch (SimplifyKeyInfo(key)) {
            case "Tab":
               map.FocusOnNextField();
               break;
            case "Shift+Tab":
               map.FocusOnPreviousField();
               break;
            case "Insert":
               _insertMode = !_insertMode;
               Console.CursorSize = _insertMode ? 1 : 100;
               break;
            default:
               ConsoleCursor cursor = new(Console.CursorLeft, Console.CursorTop);
               map.KeyPressed(key, cursor);
               switch (cursor.Mode) {
                  case ConsoleCursorMode.Offset:
                     if (cursor.Offset is (int cleft, int ctop)) {
                        Console.CursorLeft += cleft;
                        Console.CursorTop += ctop;
                     }
                     break;
                  case ConsoleCursorMode.FieldNext:
                     map.FocusOnNextField();
                     break;
                  case ConsoleCursorMode.FieldLeft:
                     map.FocusOnFieldToLeft();
                     break;
                  case ConsoleCursorMode.FieldRight:
                     map.FocusOnFieldToRight();
                     break;
                  case ConsoleCursorMode.FieldAbove:
                     map.FocusOnFieldAbove(Console.CursorLeft, Console.CursorTop);
                     break;
                  case ConsoleCursorMode.FieldBelow:
                     map.FocusOnFieldBelow(Console.CursorLeft, Console.CursorTop);
                     break;
               }
               break;
         }
         _inactiveSince = DateTime.Now;
      }
   }
}
