

// Run each test 25 times to help catch race conditions and deadlocks
// Note: TUnit's Repeat attribute may need to be applied at class or method level
// depending on TUnit version. Verify this compiles and tests run 25x.
// If assembly-level doesn't work, apply [Repeat(25)] to individual test classes.
[assembly: Repeat(25)]
