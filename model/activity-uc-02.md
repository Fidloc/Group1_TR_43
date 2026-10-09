@startuml
skinparam backgroundColor white
skinparam shadowing false
skinparam defaultFontName Arial
skinparam ArrowColor #333333

skinparam activity {
  BackgroundColor #DCE9FA
  BorderColor #3A6FB5
  FontSize 13
  DiamondBackgroundColor #FFF3B0
  DiamondBorderColor #B8A300
}

title Activity Diagram для UC-02 «Увійти до системи»

start

:**1. Відкрити сторінку входу**
(у веб-застосунку);

:**2. Ввести email і пароль**
(з форми входу);

:**3. Передати дані системі**
(відправити запит на автентифікацію);

if (Поля заповнені\nта коректні?) then (Так)
  if (Email і пароль\nвірні?) then (Так)
    :**6. Створити сесію**
    (токен / cookie);

    :**7. Відкрити головну сторінку**
    (список подій користувача);

    stop
  else (Ні)
    #F8CECC:**A2. Повідомити про невірні**
    **email або пароль**
    (вхід не виконується);
    end
  endif
else (Ні)
  #F8CECC:**A1. Повідомити про некоректні дані**
  (вхід не виконується);
  end
endif

@enduml