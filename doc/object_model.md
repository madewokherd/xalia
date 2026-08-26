# Object Model

## Elements

An *element* in Xalia is a logical UI object. Elements might correspond to windows, visible controls, or objects in an accessibility API such as MSAA or AT-SPI. They have the C# type `Xalia.UiDom.UiDomElement`.

## Element Properties

Elements can have properties, accessed in GUDL as `element.property` or, implicitly for the current element, `property`. The special name `this` refers to the current element.

When a property is accessed in GUDL, the following sources are queried, in this order:
* All providers attached to the element (using the `EvaluateIdentifier` method in C#).
* All global providers (using the `EvaluateIdentifier` method in C#).
* A set of builtin properties implemented in `UiDomElement.EvaluateIdentifierCore`, which are core to the object system.
* Properties implemented in `UiMain.EvaluateIdentifierHook`, which was separated to leave open the possibility of a different application using the Xalia object system. (TODO: This should really be a global provider instead of having a special mechanism, and it should probably be split into multiple classes.)
* An active declaration in GUDL.
* A property stored on the element using the `assign_property` action.
* All providers attached to the element (using the `EvaluateIdentifierLate` method in C#).
* All global providers (using the `EvaluateIdentifierLate` method in C#).

Some properties have special effects when declared in GUDL. These behaviors are implemented in providers and the `UiMain` and UiDomElement classes. Others may be used exclusively by GUDL code.

## The Element Tree

All active elements are in a tree called the UI DOM. An individual element may appear at most once in the tree. The root of the tree has the `UiDomRoot` type and may or may not correspond to the root object of an accessibility API.

The `UiDomElement.IsAlive` property indicates whether the element currently appears in the UI DOM. Elements that are not alive should be ignored by providers, and any in-progress operations should be silently aborted. These can unfortunately show up because we may still have references to the element when they no longer exist in the underlying accessibility API.

## Element Providers

*Element Providers* extend the functionality of elements, mostly by implementing properties. Providers must implement the `IUiDomProvider` interface and may use the `UiDomProvider` convenience class, which has no-op implementations of all `IUiDomProvider` methods. Providers may also implement other interfaces, such as `IUiDomValueProvider`, to provide more than the usual functionality.

Providers give us more flexibility than the single-inheritance model would. For example, an element on Windows might have:
* An HWND provider for window-related functions and a Button provider to work with a standard Button control.
* An HWND provider and an MSAA provider for a window that implements IAccessible.
* An MSAA provider but no HWND provider, for IAccessible children that do not have a corresponding HWND.

This system also allows providers to be added after an element is created. The HWND provider uses this to asynchronously query the window to find out what interfaces it supports.

## The Dependency Model

Most properties are not static. For example, an application can choose to show or hide its elements, move them, or reassign keyboard focus. Accessibility APIs usually (but not always) allow us to be notified when changes are made, but having to consider every change that an application can make, and manually hook up an event handler to account for these changes, would be impractical and error-prone. GUDL exists to express rules that are based on these properties, in a way that allows us to detect when we need to re-evaluate the rules based on properties that have changed.

All C# methods that evaluate GUDL expressions accept a `depends_on` argument. When a provider returns a value that could change, it adds a `(UiDomElement, GudlExpression)` pair that the caller can use to watch for conditions that might change the value. By convention, the `GudlExpression` is usually an identifier expression for the corresponding property, but there's no reason this must be the case. Sometimes when multiple properties are coupled (such as x/y/width/height of a window), providers use the same identifier for all of them, and that identifier might not be the name of any property.

Providers that do this should implement the `WatchProperty` and `UnwatchProperty` methods to keep track of which properties are being used. They can then use the `UiDomElement.PropertyChanged` method to notify the object system of updates.

Providers may also use this system to query for information lazily. If no component is using a particular property, and querying its value is expensive (usually because it means sending a request to another process), then the provider doesn't have to query it until it receives a `WatchProperty` call.

Providers that wish to monitor one or more identifier properties on their own element should implement the `GetTrackedProperties` and `TrackedPropertyChanged` methods.

In other cases, when a component wants to be notified of changes to a value, it should typically use the `ExpressionWatcher` class, which gives notifications in the form of a `Task` or C# event. This is based on the lower-level `UiDomElement.NotifyPropertyChanged` method, but that one is difficult to use correctly and not recommended.

Changes to properties do not propagate instantantly. Generally, the object system will wait for the main loop to be idle before re-evaluating GUDL for an element that needs it. This allows us to do a single evaluation if multiple updates come in quickly, and it provides a way to handle paradoxes in GUDL (such as `this_is_a_paradox: not this_is_a_paradox;`) without a stack overflow. It should be expected that, due to this delay and lazy evaluation, GUDL will at times briefly encounter stale values, or properties returning `undefined` because a query hasn't completed yet.

Because boolean operators short circuit, the order in which properties are listed in GUDL can impact performance. GUDL code should query properties that are more stable and easier to query (such as control type and window classname) before properties that are likely to change (such as window position). If we only care about the positions of buttons, we should first check whether the current element is a button, so that we never have to query the positions of non-buttons that we don't care about.

## Routines

A *routine* is a GUDL value based on the `UiDomRoutine` class. Routines can be bound to inputs. Note that a routine does not simply activate once, do its thing, and return. A routine accepts an *input stream* and responds to that stream at its own discretion. An item in the input stream may be a button state or something more complicated like a joystick position, axis position, or pixel delta.

Most routines do have an action that they activate once, when a connected input stream transitions from a non-pressed state to a pressed state, or when they recieve a "pulse" input. The `UiDomRoutineSync` and `UiDomRoutineAsync` convenience classes exist for this case.

If a connected input transitions from disconnected directly to pressed, the press is generally ignored. This is to handle a scenario where the user presses a button, triggering a routine that reassigns that button before the user releases it, preventing that one button press from doing two things.

This system allows for a lot of flexibility as routines can be chained together, modifying their input stream and passing the modified stream to another routine. For example, one can use `on_release(routine)` in GUDL to activate a routine when a button is released. The `on_release` routine will send a "pulse" input when it detects a button release. This pass-through capability is also used to implement dead-zones, and to map separate routines to individual directions on a joystick.

