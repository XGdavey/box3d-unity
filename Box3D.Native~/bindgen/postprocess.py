#!/usr/bin/env python3
"""Post-processes ClangSharp output:
1. Removes duplicate extern declarations (identical re-declarations across box3d headers).
2. Makes the UnsafeBindings class internal (raw layer is not public API; also avoids CS0050
   for externs returning internal types like BodyEventsRaw).
"""
import re
import sys

path = sys.argv[1]
with open(path) as f:
    text = f.read()

# One extern = DllImport attribute block up to and including the declaration line.
extern_pattern = re.compile(
    r"\n( +\[DllImport\(\"box3d\"[^\n]*\]\n(?: +\[[^\n]*\]\n)* +public static extern [^\n]+\n)")

seen = set()
removed = 0

def dedupe(match):
    global removed
    block = match.group(1)
    name = re.search(r"extern \S+(?: \S+)? (\w+)\(", block).group(1)
    if name in seen:
        removed += 1
        return "\n"
    seen.add(name)
    return match.group(0)

text = extern_pattern.sub(dedupe, text)

text = text.replace("public static unsafe partial class UnsafeBindings",
                    "internal static unsafe partial class UnsafeBindings")
text = text.replace("public static partial class UnsafeBindings",
                    "internal static unsafe partial class UnsafeBindings")

# Parsing on Linux resolves uint64_t to 'unsigned long', which ClangSharp maps to UIntPtr —
# semantically wrong (and invalid as const). Replace globally with ulong, EXCEPT the 3 genuine
# size_t functions (b3RecPlayer keyframe budget): size_t is 32-bit on wasm, so those must stay
# UIntPtr (platform word) — wasm-ld reports signature mismatches otherwise.
SIZE_T_FUNCTIONS = ("b3RecPlayer_SetKeyframePolicy", "b3RecPlayer_GetKeyframeBudget",
                    "b3RecPlayer_GetKeyframeBytes")
lines_out = []
for line in text.split("\n"):
    if "UIntPtr" in line and not any(fn in line for fn in SIZE_T_FUNCTIONS):
        line = line.replace("UIntPtr", "ulong")
    lines_out.append(line)
text = "\n".join(lines_out)

# b3InternalAssert is only compiled into DEBUG native builds (core.c guards it with
# !defined(NDEBUG)). Desktop lazy binding never notices, but WebGL links every extern eagerly
# and fails on the missing symbol — so drop the extern entirely.
removed_assert = 0
def drop_internal_assert(match):
    global removed_assert
    if "b3InternalAssert" in match.group(1):
        removed_assert += 1
        return "\n"
    return match.group(0)
text = extern_pattern.sub(drop_internal_assert, text)
print(f"postprocess: removed {removed_assert} debug-only extern(s)")

# Platform-aware library name: iOS needs DllImport("__Internal") (static linking). The constant
# lives in the hand-written UnsafeBindings.DllName.cs partial.
text = text.replace('[DllImport("box3d"', "[DllImport(Box3DLibrary.Name")

with open(path, "w") as f:
    f.write(text)

print(f"postprocess: {len(seen)} externs kept, {removed} duplicates removed")
