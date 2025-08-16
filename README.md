# DEFCON-LaVilla-TranslationProject
Let's expand the reach of DEFCON by bringing subtitles to other languages. This project is the core for creating a semantic search engine, and a repository for info like: imdb.com Imaigne that you can search videos by content, not only by title or author, imagine you can watch the video with subtitles in your language.

# The logic:
Videos -> Extract Audio -> WhisperX -> Generation of VTT Files (subtitles) -> Translation of that text using OpenAI -> generation of new VTT in a target language.

Remember to install FFMPEG https://ffmpeg.org/

Español:
-----

Para hacer funcionar esta nota se require de bastante paciencia, en especial por el hecho de usar CUDA

Primero creamos un virtual environment

```
python -m venv whisperx311
```

OJO, el 311 es solo el nombre.

Luego activamos el entorno virtual creado:

```
cd whisperx311
cd Scripts
activate
```

Regresamos al folder inicial e instalamos el whisperx

```
pip install whisperx
```

Procedemos a sobreescribir las librerías para que funcione exactamente como debería (fecha actual Julio 26 - 2025)

```
pip install torch torchaudio torchvision --index-url https://download.pytorch.org/whl/nightly/cu128 --force-reinstall --no-cache-dir
```

Ahora viene la parte complicada... Al ejecutar, nos da un error magistral, para solucionarlo:

https://developer.nvidia.com/cudnn-downloads Ir al archive: https://developer.nvidia.com/cudnn-archive
cudnn-windows-x86_64-8.9.7.29_cuda12-archive.zip

Extraemos el contenido de ese zip y de la carpeta "bin"

Copiamos este par de archivos:

- cudnn_ops_infer64_8
- cudnn_cnn_infer64_8

a la siguiente ruta:

```
C:\Dev\DEFCON-LaVillaProject\whisperx311\Lib\site-packages\torch\lib
```

Ahora sí, ejecutamos y ya debería funcionar bien.

English
----

Getting this to work requires a fair amount of patience, especially since it uses CUDA.

First, we create a virtual environment:

```
python -m venv whisperx311
```

NOTE: 311 is just the name.

Then, we activate the virtual environment that we just created:

```
cd whisperx311
cd Scripts
activate
```

Let's return to the initial folder and install whisperx

```
pip install whisperx
```

We proceed to overwrite the libraries so that it works exactly as it should (current date July 26 - 2025)

```
pip install torch torchaudio torchvision --index-url https://download.pytorch.org/whl/nightly/cu128 --force-reinstall --no-cache-dir
```

Now comes the tricky part... When running, it gives us a major error. In order to fix it:

https://developer.nvidia.com/cudnn-downloads Go to the archive: https://developer.nvidia.com/cudnn-archive
cudnn-windows-x86_64-8.9.7.29_cuda12-archive.zip

We extract the contents of that zip and the "bin" folder

Copy these two files:

- cudnn_ops_infer64_8
- cudnn_cnn_infer64_8

to the following path:

```
C:\Dev\DEFCON-LaVillaProject\whisperx311\Lib\site-packages\torch\lib
```

Now, run it and it should work fine.


# Ideas Generales

Primero, conseguir todos los archivos del server FTP de la DEFCON, esto será de utilidad para poder hashear los archivos.

Organizar todos los archivos en una base de datos donde se pueda apreciar:

- Ruta completa.
- Nombre de archivo.
- Tipo de archivo.
- Hash.
- Estado (en proceso, completado, no iniciado).

Ahora viene lo simpático:

- Crear una API Web donde haya un token por usuario.
- Esto es para identificar a los colaboradores.
- Conociendo los colaboradores, publicar una lista de honor por su apoyo con esta aventura.

¿Y entonces cuál es el plan?

1. Registro en un sitio web de apoyo al proyecto.
2. Obtener token (una simple GUID).
3. Ver en la web, la lista de videos que están (en proceso, completado, no iniciado).
4. Descargar los videos mediante el FTP de la DEFCON. Con esto estandarizamos las rutas y santo remedio.
5. Agregar el token al código (que todavía no está programado) y dejar procesando.
6. El resultado del proceso es enviar el archivo txt/md/json/vtt/src con el hash del video.

¿Resultado?

1. Tener un listado de los videos que tienen subtítulos en español, portugués u otro idioma.
2. Teniendo el material, nos habilita el siguiente paso que es crear resúmenes de los videos (podría ser tipo netflix!!! DEFCONFLIX xD!!!!).
3. Podemos crear un ranking de votaciones de los materiales, no solo un +1 o un -1, podríamos evaluar nivel técnico (1-5), entre otras variables.
4. Colaboración de verificación humana para las traducciones y otros textos, esto es sumamente importante para el siguiente paso.
5. Como tenemos todo el texto de cada video, podemos integrarlo con un LLM y realizar búsquedas semánticas.

# Herramienta para editar archivos VTT

Esto es algo que llevo rato trabajando para editar de manera más cómoda los archivos VTT y pues, a compartirlo también, estoy seguro que más gente le sacará provecho.
Además, tengo la certeza que teniendo todo centralizado, nos permitirá avanzar más rápido con las verificaciones de texto de los videos.
Esta parte del proyecto lo tengo en Angular y la idea es que funcione en algún server o sencillamente le den el clásico `ng server` y vaaaaamonos!

# Búsquedas semánticas

¿Te imaginas buscar sobre ESP32 y que te muestre todos los videos relacionados con ese término?

¿Y si luego quieres incluir el término "Flipper Zero"?

La búsqueda se va afinando, generando resultados que van más acorde, esto va más allá de un `"like %term%"`.

Para la UI:

- Una caja de texto con los términos.
- Una lista de tags que han entrado en la búsqueda.
- Una lista de los videos encontrados, ordenados con su ranking con respecto a los términos.

# Contacto

Si esto te interesa, quieres colaborar, ya sea con el código, con ideas, con procesamiento, con auspicio de algún tipo, ya saben cómo encontrarme (`@fredyfx` en Twitter, Telegram, Linkedin, IG).