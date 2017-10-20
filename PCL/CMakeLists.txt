cmake_minimum_required(VERSION 3.9 FATAL_ERROR)

project(plane_detection)

find_package(PCL 1.8 REQUIRED)

include_directories(${PCL_INCLUDE_DIRS})
link_directories(${PCL_LIBRARY_DIRS})
add_definitions(${PCL_DEFINITIONS})

add_executable (plane_detection plane_detection.cpp)
target_link_libraries (plane_detection ${PCL_LIBRARIES})