# LayeredFileSystem Implementation TODO

## Missing Exception Types
- [x] Add `DuplicatePathException` class (spec:249-260)
- [x] Add `LayerNotFoundException` class (spec:262-271)
- [x] Fix `LayeredFileSystemException` base class constructors (spec:243-247)

## Implementation Issues
- [x] Fix LayerContext constructor blocking with `.Result` (potential deadlock) - LayerContext.cs:41
- [x] Implement proper rollback for cached layers instead of throwing exception - LayerContext.cs:120-126
- [x] Add duplicate path validation using `PathNormalizer.HasDuplicate` method
- [x] Fix path normalization tests failing on Windows platform
- [x] Fix layer statistics calculation - file counts are 0 but byte counts work (Windows issue)
- [x] Fix sample project third layer logic - attempting to reuse first layer in same session doesn't make sense

## Missing Features (Nice to Have)
- [ ] Add cache cleanup/garbage collection functionality
- [ ] Add layer compression options for storage efficiency
- [ ] Add progress reporting for large layer operations
- [ ] Add layer verification/integrity checking
- [ ] Add support for layer metadata/annotations
- [ ] Add concurrent session safety mechanisms

## Code Quality Improvements
- [ ] Add CancellationToken support to remaining async methods
- [ ] Add comprehensive XML documentation to all public APIs
- [ ] Add validation for null/empty parameters in public methods
- [ ] Remove unused async warnings by fixing method signatures
- [ ] Remove unused parameter warning in LayerSession (tarReader)

## Testing Requirements (from spec)
- [x] **Missing**: Create integration test project/files - currently no integration tests exist
- [x] Create integration tests for full layer creation and application workflows
- [x] Add tests for whiteout file handling for deletions
- [x] Test case-insensitive path handling
- [x] Add cross-platform behavior tests
- [x] Test large file handling and streaming
- [x] Test cache hit/miss scenarios
- [x] Create `TestFileSystemBuilder` utility class (spec:288-297)

## Performance Optimizations
- [ ] Review buffer sizes for file I/O operations (spec recommends 64KB)
- [ ] Add parallel processing for independent file operations
- [ ] Optimize change detection using file modification times and sizes

## Documentation
- [x] Create README.md for the project
- [x] Add usage examples to README (basic examples included)
- [ ] Document whiteout file behavior in detail
- [ ] Add troubleshooting guide
- [ ] Add XML documentation to public APIs (partially done, needs completion)

## Recently Completed ✅
- ✅ All missing exception types (DuplicatePathException, LayerNotFoundException)
- ✅ Fixed LayerContext constructor deadlock (.Result blocking)
- ✅ Fixed layer statistics calculation (file/directory counts)
- ✅ Added duplicate path validation with proper exceptions
- ✅ Implemented proper cached layer rollback
- ✅ Fixed sample project third layer logic (cache demo with new session)
- ✅ Created comprehensive integration tests (7 new tests)
- ✅ Created TestFileSystemBuilder utility class
- ✅ Created README.md with usage examples

## Previously Completed ✅
- ✅ All core interfaces implemented
- ✅ All model classes implemented
- ✅ TAR layer reading/writing with whiteout files
- ✅ Case-insensitive path handling
- ✅ Cross-platform compatibility via System.IO.Abstractions
- ✅ Hash-based layer caching
- ✅ Directory snapshot and change detection