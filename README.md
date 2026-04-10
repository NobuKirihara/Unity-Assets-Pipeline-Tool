<h1> 1. Tab: PREFABS </h1>
&ensp;Automates the creation of prefabs from raw 3D models. <br><br>  
&emsp;<b>● Shader:</b> Set the default shader for newly created materials (Default: URP Lit). <br>
&emsp;<b>● Add Empty Root:</b> Creates an empty GameObject as a parent, useful for pivot adjustments. <br><br>
&ensp;<b> How to use:</b><br>
&emsp;● Drag FBX/OBJ files to the list or select them in the Project window. <br>
&emsp;● Click PROCESS SELECTED. <br>
&emsp;● The script creates materials, links textures, and saves the Prefab with a Pfb_ prefix (Can  change it in the script). 

<h1>2. Tab: MATERIALS</h1> 
&ensp;Assign Texture to materials matching names and PBR suffixes.<br><br>   
&emsp;<b>● Function:</b> Scans the material's folder or the selected folder for textures with matching names and PBR suffixes.<br><br>
&ensp;<b>How to use:</b><br>
&emsp;● Drag materials into the list or select them in the Project window and add a folder for scan if required. <br>
&emsp;● Click LINK SELECTED. <br>
&emsp;● The tool automatically maps link maps following Essential PBR Texture Suffixes(Extra suffixes can be added in the script).

<h1>3. Tab: TEXTURES</h1> 
&ensp;The hub for image optimization, resizing, and organization.<br><br>  
&emsp;<b>● Base Name</b> If filled, renames textures to Tex_”Name”_01_Albedo. If empty, keep original names.<br> 
&emsp;<b>● Resize Assets </b>scales images (128px to 4096px). <br>
&emsp;<b>● Auto-Standardize </b>change images suffixes to script Default <br>
&emsp;<b>● Backup Originals </b>if enabled move the original image to Assets/Old_Assets/IMG and places optimized versions in the source 
&emsp;folder. if disabled overwrites the original file with the optimized version.<br><br> 
&ensp;<b>How to use:</b> <br>
&emsp;● Set the Base Name. <br>
&emsp;● Select desired resolution. <br>
&emsp;● Toggle "Backup Originals" if needed. <br>
&emsp;● Click PROCESS SELECTED.
