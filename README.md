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

Ahora instalamos el whisperx

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
cudnn_ops_infer64_8
cudnn_cnn_infer64_8

a la siguiente ruta:

```
C:\Dev\VTT-Udemy-Courses-fredyfx\whisperx311\Lib\site-packages\torch\lib
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

Now we install whisperx

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
cudnn_ops_infer64_8
cudnn_cnn_infer64_8

to the following path:

```
C:\Dev\VTT-Udemy-Courses-fredyfx\whisperx311\Lib\site-packages\torch\lib
```

Now, run it and it should work fine.