# FilmrecAPI

# Deploy to heroku:
- heroku container:login
- docker build -t filmrec
- heroku container:push -a filmrec web
- heroku container:release -a filmrec web