import re

with open('C:/Users/Michael/Downloads/Projects/Aimmy-Aimmy-V2/Aimmy-Aimmy-V2/Aimmy2/MainWindow_from_log.txt', 'r', encoding='utf-8') as f:
    lines = f.readlines()

output_lines = []
start_collecting = False
for line in lines:
    if line.strip().startswith('1: <Window x:Class="Aimmy2.MainWindow"'):
        start_collecting = True
    if start_collecting:
        # Check if line matches the pattern \d+:
        match = re.match(r'^\d+: (.*)', line)
        if match:
            output_lines.append(match.group(1) + '\n')
        elif re.match(r'^\d+:$', line.strip()):
            output_lines.append('\n')

with open('C:/Users/Michael/Downloads/Projects/Aimmy-Aimmy-V2/Aimmy-Aimmy-V2/Aimmy2/MainWindow.xaml', 'w', encoding='utf-8') as f:
    f.writelines(output_lines)
print('Wrote to MainWindow.xaml successfully, length:', len(output_lines))
