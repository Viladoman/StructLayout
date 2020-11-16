cmake_minimum_required(VERSION 3.5)

project(StructLayoutTest VERSION 1.0.0 LANGUAGES CXX)

###########
# Options #
###########

set(MAIN_TARGET StructLayoutTest)

###########
# Globals #
###########

set(BASE_CODE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/src)
set(BASE_INCLUDE_PATHS ${BASE_CODE_DIR})

#########
# Utils #
#########

function(BaseCompilerSetup TARGET_NAME)

	#include paths
    target_include_directories(${TARGET_NAME} PRIVATE ${BASE_INCLUDE_PATHS})

	# Compiler flags #
	target_compile_features(${TARGET_NAME} PRIVATE cxx_std_17)

	# Preprocessor 
	if(CMAKE_BUILD_TYPE STREQUAL "Debug")
	  target_compile_definitions(${TARGET_NAME} PRIVATE TARGET_DEBUG=1 )
	elseif(CMAKE_BUILD_TYPE STREQUAL "Release" OR CMAKE_BUILD_TYPE STREQUAL "RelWithDebInfo")
	  target_compile_definitions(${TARGET_NAME} PRIVATE TARGET_RELEASE=1 )
	endif()
endfunction()

###############
# Main Target #
###############

#Common settings for MAIN_TARGET
add_executable(${MAIN_TARGET})
target_sources(${MAIN_TARGET} PRIVATE "src/main.cpp")

BaseCompilerSetup(${MAIN_TARGET})
