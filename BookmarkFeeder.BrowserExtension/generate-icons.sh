#!/bin/bash

# Create icons directory if it doesn't exist
mkdir -p icons

# Generate placeholder icons with a simple bookmark design
for size in 16 48 128; do
    convert -size ${size}x${size} xc:transparent \
        -fill "#4B5563" \
        -draw "rectangle 0,0 ${size},${size}" \
        -fill "#FFFFFF" \
        -draw "path 'M ${size}/4},${size}/8} L ${size}*3/4},${size}/8} L ${size}*3/4},${size}*7/8} L ${size}/4},${size}*7/8} Z'" \
        -draw "path 'M ${size}/2},${size}/8} L ${size}/2},${size}*7/8}'" \
        icons/icon${size}.png
done 