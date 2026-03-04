using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace InputManager
{
    /// <summary>
    /// 다양한 플랫폼의 입력을 처리하여 <c>InputEvent</c>로 변환하는 클래스.
    /// InputManager는 마우스/포인터 입력과 같이 X, Y값이 존재하는 입력은 다루지 않습니다.
    /// 키보드나 컨트롤러 등의 키 입력과 같이 입력을 묘사하는 별도의 값이 없는 입력만 다룹니다.</br>
    /// 그 입력들이 들어왔을 때, 입력을 InputEvent로 변환하여 StackManager에 전달하는 역할을 합니다. </br>
    /// 
    /// PC만 지원하는 구조입니다.
    /// </summary>
    public class InputManager
    {
        private static InputManager _instance;
        public static InputManager Instance
        {
            get
            {
                if (_instance == null)
                    throw new InvalidOperationException(
                        "InputManager is not initialized. Call Initialize() first.");
                return _instance;
            }
        }

        /// <summary>
        /// 입력 덱. 들어온 입력을 보관하고 관리하는데 사용합니다.
        /// Collect 함수와 GameState의 함수들이 입력 덱에 입력을 추가할 수 있습니다.
        /// 
        /// </summary>
        private readonly List<InputEvent> _pendingInputs = new();

        /// <summary>
        /// 입력 덱의 크기 제한을 나타내는 상수 값.
        /// </summary>
        private const int InputDequeLimit = 2;

        /// <summary>
        /// 키보드의 바인딩을 저장하는 Dictionary. <키의 종류, InputEvent를 생성하는 함수> 를 갖습니다.
        /// </summary>
        private readonly Dictionary<Key, Func<InputEvent>> _keyboardBindings = new();

        /// <summary>
        /// 입력을 처리할 키의 종류를 저장하는 HashSet.
        /// </summary>
        private readonly HashSet<Key> _keyboardKeys = new();

        /// <summary>
        /// InputType의 우선도를 저장하는 Dictionary. int 값이 낮을 수록 우선도가 높습니다.
        /// </summary>
        private readonly Dictionary<InputType, int> _inputTypePrecedence = new()
        {
            {InputType.Pointer, 0},

            {InputType.Escape, 2},
            {InputType.Confirm, 3},
            {InputType.Menu, 4},

            {InputType.Left, 10},
            {InputType.Right, 11},
            {InputType.Up, 12},
            {InputType.Down, 13},

            {InputType.Select, 20},
        };

        private InputManager()
        {
            // 

            _keyboardKeys.AddRange(new List<Key>(){
                Key.Escape,
                Key.Q,
                Key.DownArrow,
                Key.UpArrow,
                Key.LeftArrow,
                Key.RightArrow,
                Key.Space,
                Key.Digit1,
                Key.Digit2,
                Key.Digit3,
                Key.Digit4,
                Key.Digit5,
                Key.Digit6,
                Key.Digit7,
            });

            _keyboardBindings.Add(Key.Escape, () => InputEvent.Escape());
            _keyboardBindings.Add(Key.Q, () => InputEvent.Menu());
            _keyboardBindings.Add(Key.DownArrow, () => InputEvent.ArrowDown());
            _keyboardBindings.Add(Key.UpArrow, () => InputEvent.ArrowUp());
            _keyboardBindings.Add(Key.LeftArrow, () => InputEvent.ArrowLeft());
            _keyboardBindings.Add(Key.RightArrow, () => InputEvent.ArrowRight());
            _keyboardBindings.Add(Key.Space, () => InputEvent.Confirm());

            for (int i = 0; i < 7; i++)
            {
                int idx = i;
                InputEvent func() => InputEvent.Select(idx);

                _keyboardBindings.Add(Key.Digit1 + idx, func);
            }
        }

        /// <summary>
        /// InputManager를 초기화합니다. 딱 한 번, GameRoot에서 호출합니다. 그 외에는 호출하면 안됩니다.
        /// </summary>
        public static void Initialize()
        {
            if (_instance != null)
                throw new InvalidOperationException("InputManager is already initialized.");

            _instance = new();
        }

        /// <summary>
        /// I/O 장치로부터 입력을 받아 InputEvent로 변환하여 입력 덱에 담습니다. GameRoot.Update()에서 매 프레임마다 호출됩니다.
        /// </summary>
        public void CollectDeviceInput()
        {
            Keyboard keyboard = Keyboard.current;

            InputEvent inputToPush = null;
            int precedence = 99999;

            // 미리 등록된 키들이 입력되었는지 순회하면서, 입력이 있다면 입력을 저장합니다.
            // 동시 입력이 거의 필요하지 않을 것으로 예상되는 게임 구조이므로, 
            // 같은 프레임에 여러 입력이 동시에 입력되면 가장 우선되는 입력 하나만을 저장합니다.
            // 비슷한 이유로 키가 Press, Release 되는 두 경우만 다룹니다.
            foreach (Key key in _keyboardKeys)
            {
                KeyControl control = keyboard[key];

                if (control.wasPressedThisFrame)
                {

                    InputEvent translated = TranslateKeyboard(key);
                    int detectedPrecedence = _inputTypePrecedence[translated.Type];

                    if (precedence > detectedPrecedence)
                    {
                        inputToPush = translated;
                        precedence = detectedPrecedence;
                    }

                }
                else if (control.wasReleasedThisFrame)
                {
                    // TODO
                }
            }

            if (inputToPush is not null)
            {
                RaiseInput(inputToPush);
            }
        }

        /// <summary>
        /// 입력 덱의 가장 앞의 InputEvent를 뽑아 반환합니다. 덱이 비어있다면 null을 반환합니다.
        /// InputManager와 UniversalController의 작업 순서를 일관적으로 유지하기 위해, 한 프레임에 하나의 InputEvent만 처리하도록 되어있습니다.
        /// </summary>
        public InputEvent GetPendingInput()
        {
            if (_pendingInputs.Count == 0)
            {
                return null;
            }

            InputEvent front = _pendingInputs.First();
            _pendingInputs.RemoveAt(0);

            return front;
        }

        /// <summary>
        /// 주어진 키보드 키 입력을 그에 맞는 InputEvent로 변환합니다.
        /// </summary>
        /// <returns></returns>
        private InputEvent TranslateKeyboard(Key key)
        {
            if (!_keyboardBindings.TryGetValue(key, out Func<InputEvent> func))
            {
                return null;
            }

            if (func is null)
            {
                return null;
            }

            return func();
        }

        /// <summary>
        /// 입력 덱의 뒤에 InputEvent를 하나 추가합니다.
        /// </summary>
        public bool RaiseInput(InputEvent e)
        {
            if (_pendingInputs.Count >= InputDequeLimit)
            {
                return false;
            }

            _pendingInputs.Add(e);

            return true;
        }
    }

    /// <summary>
    /// 추상 입력을 표현하는 클래스. 키보드, 마우스 등의 물리적 입력을 그대로 사용하면 멀티플랫폼 지원이 어려우므로,
    /// 물리적 입력을 추상화하여 State가 처리할 입력의 종류를 추상 입력으로 제한하는 역할을 수행합니다.
    /// 
    /// 리팩토링이 필요한 구조입니다.
    /// </summary>
    public class InputEvent
    {
        public InputType Type;
        public int V1;
        public int V2;
        public int V3;
        public int V4;

        public InputEvent(InputType type, int v1, int v2, int v3, int v4)
        {
            Type = type;
            V1 = v1;
            V2 = v2;
            V3 = v3;
            V4 = v4;
        }

        public InputEvent(InputType type, int v1, int v2, int v3) : this(type, v1, v2, v3, 0) { }
        public InputEvent(InputType type, int v1, int v2) : this(type, v1, v2, 0, 0) { }
        public InputEvent(InputType type, int v1) : this(type, v1, 0, 0, 0) { }
        public InputEvent(InputType type) : this(type, 0, 0, 0, 0) { }

        public static InputEvent Pointer(int x)
        {
            return new(InputType.Pointer, x);
        }

        public static InputEvent Pointer(int x, int y)
        {
            return new(InputType.Pointer, x, y);
        }

        public static InputEvent Pointer(int x, int y, int z)
        {
            return new(InputType.Pointer, x, y, z);
        }

        public static InputEvent Pointer(int x, int y, int z, int w)
        {
            return new(InputType.Pointer, x, y, z, w);
        }

        public static InputEvent Escape()
        {
            return new(InputType.Escape);
        }

        public static InputEvent Confirm()
        {
            return new(InputType.Confirm);
        }

        public static InputEvent ArrowUp()
        {
            return new(InputType.Up);
        }

        public static InputEvent ArrowDown()
        {
            return new(InputType.Down);
        }

        public static InputEvent ArrowLeft()
        {
            return new(InputType.Left);
        }

        public static InputEvent ArrowRight()
        {
            return new(InputType.Right);
        }

        public static InputEvent Select(int index)
        {
            return new(InputType.Select, index);
        }

        public static InputEvent Menu()
        {
            return new(InputType.Menu);
        }
    }

    public enum InputType
    {
        Confirm,

        Up,
        Left,
        Right,
        Down,
        Menu,
        Escape,
        Select,

        /// <summary>
        /// 수가 엄청 많은 Input을 처리할 때 사용하는 타입. 마우스 포인터의 X, Y 값을 그대로 넣기보단 상황에 맞춰 row, column 등으로 변환해서 사용하세요.
        /// </summary>
        Pointer,
    }
}