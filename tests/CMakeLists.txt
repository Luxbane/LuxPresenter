# Runtime DLLs next to the test executables (PDF engine, WebView2 loader).
function(lux_test_runtime target)
    foreach(dll "${LUX_PDFIUM_DLL}" "${LUX_WEBVIEW2_LOADER_DLL}" "${CMAKE_SOURCE_DIR}/third_party/mpv-lgpl/libmpv-2.dll" "${CMAKE_SOURCE_DIR}/third_party/mpv-lgpl/vulkan-1.dll")
        if(EXISTS "${dll}")
            add_custom_command(TARGET ${target} POST_BUILD COMMAND ${CMAKE_COMMAND} -E copy_if_different "${dll}" "$<TARGET_FILE_DIR:${target}>")
        endif()
    endforeach()
endfunction()

set(LUX_TEST_DEFS
    LUX_TEST_TMP="${CMAKE_BINARY_DIR}/test-tmp"
    LUX_DATA_SRC="${CMAKE_SOURCE_DIR}/data"
    LUX_FIXTURES="${CMAKE_SOURCE_DIR}/tests/fixtures"
    LUX_PDFIUM_DLL="${LUX_PDFIUM_DLL}"
    LUX_BUNDLED_LO="${LUX_PORTABLE_DIR}/Engines/LibreOffice"
    LUX_BUNDLED_WV2="${LUX_PORTABLE_DIR}/Engines/WebView2")

# M1: models, JSON persistence, paths (no GUI)
add_executable(luxcore_tests test_core.cpp)
target_link_libraries(luxcore_tests PRIVATE luxcore Qt6::Test)
target_compile_definitions(luxcore_tests PRIVATE ${LUX_TEST_DEFS})
set_target_properties(luxcore_tests PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
add_test(NAME luxcore_tests COMMAND luxcore_tests)
set_tests_properties(luxcore_tests PROPERTIES
    ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:${LUX_QT_BIN}")

# M3: song search, schedule service, backups, sample songs, JSON compatibility
add_executable(luxsongs_tests test_songs_schedule.cpp)
target_link_libraries(luxsongs_tests PRIVATE luxcore Qt6::Test)
target_compile_definitions(luxsongs_tests PRIVATE ${LUX_TEST_DEFS})
set_target_properties(luxsongs_tests PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
add_test(NAME luxsongs_tests COMMAND luxsongs_tests)
set_tests_properties(luxsongs_tests PROPERTIES
    ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:${LUX_QT_BIN}")

# M2: main window, navigation, theme (Qt offscreen platform, no visible windows)
add_executable(luxui_tests test_ui.cpp)
target_link_libraries(luxui_tests PRIVATE luxui Qt6::Test)
target_compile_definitions(luxui_tests PRIVATE ${LUX_TEST_DEFS})
set_target_properties(luxui_tests PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
add_test(NAME luxui_tests COMMAND luxui_tests)
set_tests_properties(luxui_tests PROPERTIES
    ENVIRONMENT "QT_QPA_PLATFORM=offscreen;QT_QPA_FONTDIR=C:/Windows/Fonts"
    ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:${LUX_QT_BIN}")

# M4: display selection, projection state/output sync, transition pipeline, scene rendering (offscreen)
add_executable(luxprojection_tests test_projection.cpp)
target_link_libraries(luxprojection_tests PRIVATE luxui Qt6::Test)
target_compile_definitions(luxprojection_tests PRIVATE ${LUX_TEST_DEFS})
set_target_properties(luxprojection_tests PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
add_test(NAME luxprojection_tests COMMAND luxprojection_tests)
set_tests_properties(luxprojection_tests PROPERTIES
    ENVIRONMENT "QT_QPA_PLATFORM=offscreen;QT_QPA_FONTDIR=C:/Windows/Fonts"
    ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:${LUX_QT_BIN}")

# M4: real Win32 windows (placement, z-order, styles, rendering) and the dual-deck libmpv engine.
# Runs on the "windows" platform; the windows are small and shown only briefly.
add_executable(luxwindows_tests test_windows.cpp)
target_link_libraries(luxwindows_tests PRIVATE luxui Qt6::Test)
target_compile_definitions(luxwindows_tests PRIVATE ${LUX_TEST_DEFS}
    LUX_MPV_DLL="${CMAKE_SOURCE_DIR}/third_party/mpv-lgpl/libmpv-2.dll")
set_target_properties(luxwindows_tests PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
add_dependencies(luxwindows_tests LuxPresenter)
add_test(NAME luxwindows_tests COMMAND luxwindows_tests)
set_tests_properties(luxwindows_tests PROPERTIES
    ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:${LUX_QT_BIN};QT_QPA_PLATFORM=unset:")

# M5: PDF engine, LibreOffice conversion (fake soffice), presentation service, projector + MainWindow integration
add_executable(fake_soffice fake_soffice.cpp)
set_target_properties(fake_soffice PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
add_executable(luxpresentation_tests test_presentation.cpp)
target_link_libraries(luxpresentation_tests PRIVATE luxui Qt6::Test)
target_compile_definitions(luxpresentation_tests PRIVATE ${LUX_TEST_DEFS} LUX_FAKE_SOFFICE="$<TARGET_FILE:fake_soffice>")
set_target_properties(luxpresentation_tests PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
add_dependencies(luxpresentation_tests fake_soffice LuxPresenter)
lux_test_runtime(luxpresentation_tests)
add_test(NAME luxpresentation_tests COMMAND luxpresentation_tests)
set_tests_properties(luxpresentation_tests PROPERTIES
    ENVIRONMENT "QT_QPA_PLATFORM=offscreen;QT_QPA_FONTDIR=C:/Windows/Fonts"
    ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:${LUX_QT_BIN}")
lux_test_runtime(luxui_tests)
lux_test_runtime(luxwindows_tests)
lux_test_runtime(luxprojection_tests)# M6: options, backgrounds, Bible, ad blocker (offscreen)
add_executable(luxm6_tests test_m6.cpp)
target_link_libraries(luxm6_tests PRIVATE luxui Qt6::Test)
target_compile_definitions(luxm6_tests PRIVATE ${LUX_TEST_DEFS} LUX_MPV_DLL="${CMAKE_SOURCE_DIR}/third_party/mpv-lgpl/libmpv-2.dll")
set_target_properties(luxm6_tests PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
lux_test_runtime(luxm6_tests)
add_test(NAME luxm6_tests COMMAND luxm6_tests)
set_tests_properties(luxm6_tests PROPERTIES
    ENVIRONMENT "QT_QPA_PLATFORM=offscreen;QT_QPA_FONTDIR=C:/Windows/Fonts"
    ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:${LUX_QT_BIN}")
