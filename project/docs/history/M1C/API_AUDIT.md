# M1C native API audit

149 method/accessor signatures match the previously supplied pinned generated
wrappers. New checked surfaces: QuickActions.Position; modern and legacy tech
popup data entry points; TechData.unitUnlocks; Il2CppSystem.Object.MemberwiseClone.
See `evidence/native-api-inspection.json` and `tools/native-wheel-api.json`.

The production adapter remains reflection-bound, except its existing typed native
result/argument references. The display prefix replaces a TechData argument with
a separately allocated native display copy, never a raw pointer write.

Metadata checking is not C# execution, native code validation or visual testing.
The managed adapter fixture uses stand-ins and does not install Harmony hooks in
Unity. This limit is explicit in its output and build receipt.
